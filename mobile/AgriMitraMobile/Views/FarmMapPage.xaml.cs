using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Maps;
using AgriMitraMobile.ViewModels;

namespace AgriMitraMobile.Views;

public partial class FarmMapPage : ContentPage
{
    private readonly FarmMapViewModel _vm;
    private readonly List<Pin>        _pins    = new();
    private Polyline?                 _outline;

    public FarmMapPage(FarmMapViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;

        // Center on Kankavli, Sindhudurg
        FarmMap.MoveToRegion(MapSpan.FromCenterAndRadius(
            new Location(16.55, 73.72),
            Distance.FromKilometers(5)));
    }

    private void OnMapClicked(object? sender, MapClickedEventArgs e)
    {
        _vm.AddVertex(e.Location.Latitude, e.Location.Longitude);
        RefreshOverlay();
    }

    private void RefreshOverlay()
    {
        // Remove old pins and polyline
        foreach (var p in _pins) FarmMap.Pins.Remove(p);
        _pins.Clear();
        if (_outline != null) FarmMap.MapElements.Remove(_outline);

        var verts = _vm.Vertices;
        if (verts.Count == 0) return;

        // Add vertex pins
        for (int i = 0; i < verts.Count; i++)
        {
            var pin = new Pin
            {
                Label    = $"P{i + 1}",
                Location = verts[i],
                Type     = PinType.Generic,
            };
            FarmMap.Pins.Add(pin);
            _pins.Add(pin);
        }

        // Draw outline polyline (close if ≥3 points)
        var line = new Polyline { StrokeColor = Color.FromArgb("#FFFF00"), StrokeWidth = 3 };
        foreach (var v in verts) line.Geopath.Add(v);
        if (verts.Count >= 3) line.Geopath.Add(verts[0]); // close
        FarmMap.MapElements.Add(line);
        _outline = line;
    }
}

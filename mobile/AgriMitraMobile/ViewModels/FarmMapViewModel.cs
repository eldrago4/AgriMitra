using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Maps;
using System.Collections.ObjectModel;
using AgriMitraMobile.Models;
using AgriMitraMobile.Services;

namespace AgriMitraMobile.ViewModels;

public partial class FarmMapViewModel : BaseViewModel
{
    private readonly ILocalDatabaseService _db;

    [ObservableProperty] private double  _areaHectares;
    [ObservableProperty] private string  _areaText      = "Tap on the map to mark field boundary";
    [ObservableProperty] private string  _polygonStatus = "0 points selected";
    [ObservableProperty] private bool    _canConfirm;
    [ObservableProperty] private bool    _canUndo;
    [ObservableProperty] private string  _fieldLabel    = string.Empty;

    public ObservableCollection<Location> Vertices { get; } = new();

    public FarmMapViewModel(ILocalDatabaseService db)
    {
        _db   = db;
        Title = "Draw Farm Boundary";
    }

    public void AddVertex(double lat, double lon)
    {
        Vertices.Add(new Location(lat, lon));
        UpdateArea();
    }

    public void UndoLastVertex()
    {
        if (Vertices.Count == 0) return;
        Vertices.RemoveAt(Vertices.Count - 1);
        UpdateArea();
    }

    private void UpdateArea()
    {
        CanUndo = Vertices.Count > 0;
        PolygonStatus = $"{Vertices.Count} point{(Vertices.Count == 1 ? "" : "s")} selected";

        if (Vertices.Count >= 3)
        {
            AreaHectares = CalculateAreaHectares();
            AreaText     = $"Area: {AreaHectares:F2} ha  ({AreaHectares * 2.471:F2} acres)";
            CanConfirm   = true;
        }
        else
        {
            AreaText   = Vertices.Count == 0
                ? "Tap on the map to mark field boundary"
                : $"Add {3 - Vertices.Count} more point{(Vertices.Count == 2 ? "" : "s")}";
            CanConfirm = false;
        }
    }

    // Shoelace formula on lat/lon → approximate hectares
    private double CalculateAreaHectares()
    {
        if (Vertices.Count < 3) return 0;
        double area = 0;
        int n = Vertices.Count;
        const double R = 6_371_000; // metres

        for (int i = 0; i < n; i++)
        {
            var p1 = Vertices[i];
            var p2 = Vertices[(i + 1) % n];
            double lat1 = p1.Latitude  * Math.PI / 180;
            double lat2 = p2.Latitude  * Math.PI / 180;
            double lon1 = p1.Longitude * Math.PI / 180;
            double lon2 = p2.Longitude * Math.PI / 180;
            area += (lon2 - lon1) * (2 + Math.Sin(lat1) + Math.Sin(lat2));
        }

        double areaM2 = Math.Abs(area) * R * R / 2;
        return areaM2 / 10_000;
    }

    private string BuildGeoJson()
    {
        var coords = Vertices
            .Append(Vertices[0]) // close ring
            .Select(v => $"[{v.Longitude},{v.Latitude}]");
        return $"{{\"type\":\"Polygon\",\"coordinates\":[[{string.Join(",", coords)}]]}}";
    }

    [RelayCommand]
    private void Clear()
    {
        Vertices.Clear();
        UpdateArea();
    }

    [RelayCommand(CanExecute = nameof(CanConfirm))]
    private async Task ConfirmAsync()
    {
        string label = string.IsNullOrWhiteSpace(FieldLabel)
            ? $"Field {DateTime.Now:ddMMyy-HHmm}"
            : FieldLabel.Trim();

        var field = new LocalField
        {
            Label          = label,
            PolygonGeoJson = BuildGeoJson(),
            AreaHectares   = AreaHectares,
            CreatedAt      = DateTime.UtcNow,
        };

        await _db.SaveFieldAsync(field);

        // Navigate to crop details with field id
        await Shell.Current.GoToAsync($"cropdetails?fieldId={field.Id}&area={AreaHectares:F2}");
    }
}

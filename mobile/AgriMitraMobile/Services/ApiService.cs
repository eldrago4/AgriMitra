using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AgriMitraMobile.Models;

namespace AgriMitraMobile.Services;

public interface IApiService
{
    Task<PredictionResult?> PredictAsync(PredictRequest request);
    Task<string?>           RegisterFarmerAsync(FarmerProfile farmer);
    Task<string?>           SaveFieldAsync(LocalField field, string farmerId);
    Task<MspData?>          GetMspDataAsync();
    Task<MandiResult?>      GetNearestMandiAsync(double lat, double lon);
}

// ── DTOs ──────────────────────────────────────────────────────────────────────

public record PredictRequest(
    List<List<double>> FarmCoordinates,
    string             CropType,
    IoTSensorData?     IotSensorData,
    string?            PlantingDate,
    string?            FieldId
);

public record IoTSensorData(
    float SoilN, float SoilP, float SoilK,
    float Moisture, float Ph, float Temperature,
    float Ndvi = 0.6f, float Elevation = 100f
);

public record MspData(string Year, List<MspEntry> Data);

// Accepts both "price" (updated route.ts) and "msp" (legacy deployed API)
public class MspEntry
{
    public string  Crop    { get; set; } = "";
    public string? Variety { get; set; }
    public string? Group   { get; set; }
    public string? Unit    { get; set; }

    private int _price;
    public int Price
    {
        get => _price;
        set { if (_price == 0) _price = value; }
    }
    [JsonPropertyName("msp")]
    public int Msp
    {
        get => _price;
        set { if (_price == 0) _price = value; }
    }

    public MspEntry() { }
    public MspEntry(string crop, string? variety, int price, string? unit)
    {
        Crop = crop; Variety = variety; _price = price; Unit = unit;
    }
}
public record MandiResult(MandiEntry Nearest, List<MandiEntry> All);
public record MandiEntry(string Name, string District, string State,
                          double Lat, double Lon, double DistanceKm,
                          string Phone, bool IsOpen);

// ── Service implementation ────────────────────────────────────────────────────

public class ApiService : IApiService
{
    private const string BaseUrl = "https://1ved.cloud";

    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public ApiService()
    {
        _http = new HttpClient
        {
            BaseAddress = new Uri(BaseUrl),
            Timeout     = TimeSpan.FromSeconds(30),
        };
    }

    public async Task<PredictionResult?> PredictAsync(PredictRequest request)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync("/api/predict", request, _jsonOpts);
            resp.EnsureSuccessStatusCode();
            return await resp.Content.ReadFromJsonAsync<PredictionResult>(_jsonOpts);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"PredictAsync error: {ex.Message}");
            return null;
        }
    }

    public async Task<string?> RegisterFarmerAsync(FarmerProfile farmer)
    {
        try
        {
            var body = new { name = farmer.Name, phone = farmer.Phone,
                              village = farmer.Village, district = farmer.District,
                              language = farmer.Language };
            var resp = await _http.PostAsJsonAsync("/api/farmers", body);
            resp.EnsureSuccessStatusCode();
            var result = await resp.Content.ReadFromJsonAsync<JsonElement>();
            return result.GetProperty("id").GetString();
        }
        catch { return null; }
    }

    public async Task<string?> SaveFieldAsync(LocalField field, string farmerId)
    {
        try
        {
            var body = new { farmerId, polygonGeoJson = field.PolygonGeoJson,
                              areaHectares = field.AreaHectares, label = field.Label };
            var resp = await _http.PostAsJsonAsync("/api/fields", body);
            resp.EnsureSuccessStatusCode();
            var result = await resp.Content.ReadFromJsonAsync<JsonElement>();
            return result.GetProperty("id").GetString();
        }
        catch { return null; }
    }

    public async Task<MspData?> GetMspDataAsync()
    {
        try
        {
            return await _http.GetFromJsonAsync<MspData>("/api/market/msp", _jsonOpts);
        }
        catch { return null; }
    }

    public async Task<MandiResult?> GetNearestMandiAsync(double lat, double lon)
    {
        try
        {
            return await _http.GetFromJsonAsync<MandiResult>(
                $"/api/market/mandi?lat={lat}&lon={lon}", _jsonOpts);
        }
        catch { return null; }
    }
}

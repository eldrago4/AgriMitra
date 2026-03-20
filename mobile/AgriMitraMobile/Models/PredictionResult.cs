namespace AgriMitraMobile.Models;

/// <summary>DTO matching the /api/predict response shape.</summary>
public class PredictionResult
{
    public float  PredictedYield     { get; set; }
    public float  UncertaintyBand    { get; set; }
    public string ModelVersion       { get; set; } = string.Empty;
    public int    InferenceLatencyMs { get; set; }
    public string FertilizerAdvisory { get; set; } = string.Empty;
    public string IrrigationAdvisory { get; set; } = string.Empty;
    public string MarketAdvisory     { get; set; } = string.Empty;
    public bool   IsOffline          { get; set; }
    public string CropType           { get; set; } = string.Empty;
    public float  DistrictAvgYield   { get; set; } = 16.1f;  // Maharashtra average
}

using SQLite;

namespace AgriMitraMobile.Models;

[Table("LocalPredictions")]
public class LocalPrediction
{
    [PrimaryKey, AutoIncrement]
    public int      Id                  { get; set; }
    public int      FieldId             { get; set; }   // FK → LocalField.Id
    public string   CropType            { get; set; } = string.Empty;
    public string?  Season              { get; set; }
    public string?  PlantingDate        { get; set; }
    public float    PredictedYield      { get; set; }
    public float    UncertaintyBand     { get; set; }
    public string   FertilizerAdvisory  { get; set; } = string.Empty;
    public string   IrrigationAdvisory  { get; set; } = string.Empty;
    public string   MarketAdvisory      { get; set; } = string.Empty;
    public int      InferenceLatencyMs  { get; set; }
    public string   ModelVersion        { get; set; } = string.Empty;
    public bool     IsOffline           { get; set; }
    public DateTime CreatedAt           { get; set; } = DateTime.UtcNow;
}

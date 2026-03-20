using SQLite;

namespace AgriMitraMobile.Models;

[Table("LocalFields")]
public class LocalField
{
    [PrimaryKey, AutoIncrement]
    public int      Id               { get; set; }
    public string   FarmerId         { get; set; } = string.Empty;
    public string   Label            { get; set; } = "My Field";
    public string   PolygonGeoJson   { get; set; } = string.Empty;
    public double   AreaHectares     { get; set; }
    public string   LastCropType     { get; set; } = string.Empty;
    public float    LastPredictedYield { get; set; }
    public DateTime CreatedAt        { get; set; } = DateTime.UtcNow;
    public DateTime LastPredictionAt { get; set; }
    // Server-side ID after sync
    public string?  ServerId         { get; set; }
}

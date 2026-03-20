namespace AgriMitraMobile.Models;

/// <summary>Farmer profile stored in Preferences (lightweight, not SQLite).</summary>
public class FarmerProfile
{
    public string Name     { get; set; } = string.Empty;
    public string Phone    { get; set; } = string.Empty;
    public string Village  { get; set; } = string.Empty;
    public string District { get; set; } = string.Empty;
    public string Language { get; set; } = "en";
    public string? ServerId { get; set; }

    public static FarmerProfile Load()
    {
        return new FarmerProfile
        {
            Name      = Preferences.Get("farmer_name",     string.Empty),
            Phone     = Preferences.Get("farmer_phone",    string.Empty),
            Village   = Preferences.Get("farmer_village",  string.Empty),
            District  = Preferences.Get("farmer_district", string.Empty),
            Language  = Preferences.Get("farmer_language", "en"),
            ServerId  = Preferences.Get("farmer_server_id", (string?)null),
        };
    }

    public void Save()
    {
        Preferences.Set("farmer_name",      Name);
        Preferences.Set("farmer_phone",     Phone);
        Preferences.Set("farmer_village",   Village);
        Preferences.Set("farmer_district",  District);
        Preferences.Set("farmer_language",  Language);
        if (ServerId != null) Preferences.Set("farmer_server_id", ServerId);
    }

    public bool IsRegistered => !string.IsNullOrEmpty(Name) && !string.IsNullOrEmpty(Phone);

    public static void Clear()
    {
        Preferences.Remove("farmer_name");
        Preferences.Remove("farmer_phone");
        Preferences.Remove("farmer_village");
        Preferences.Remove("farmer_district");
        Preferences.Remove("farmer_language");
        Preferences.Remove("farmer_server_id");
    }
}

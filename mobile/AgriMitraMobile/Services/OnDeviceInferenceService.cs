using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using AgriMitraMobile.Models;

namespace AgriMitraMobile.Services;

public interface IOnDeviceInferenceService
{
    Task<PredictionResult?> PredictAsync(string cropType, float[]? iotFeatures,
                                          string? plantingDate,
                                          string? season = null,
                                          string? irrigationType = null);
    bool IsModelLoaded { get; }
}

public class OnDeviceInferenceService : IOnDeviceInferenceService, IDisposable
{
    private InferenceSession? _session;
    private readonly string   _modelFileName = "agrimitra_int8.onnx";

    private const int N_CHANNELS   = 4;
    private const int N_WEEKS      = 12;
    private const int IMG_H        = 224;
    private const int IMG_W        = 224;
    private const int NODE_FEAT    = 8;
    private const int GRAPH_NODES  = 3;

    public bool IsModelLoaded => _session is not null;

    // 12-week NDVI profiles (synthetic, crop-specific)
    private static readonly Dictionary<string, float[]> NdviCurves = new()
    {
        ["Paddy(Deshaj)"]  = new[] {0.12f,0.18f,0.30f,0.48f,0.62f,0.72f,0.78f,0.80f,0.78f,0.70f,0.55f,0.38f},
        ["Coconut"]        = new[] {0.60f,0.62f,0.63f,0.64f,0.65f,0.65f,0.64f,0.63f,0.62f,0.61f,0.60f,0.60f},
        ["Cashewnuts"]     = new[] {0.50f,0.45f,0.38f,0.32f,0.30f,0.38f,0.48f,0.57f,0.62f,0.60f,0.55f,0.52f},
        ["Arecanut"]       = new[] {0.55f,0.56f,0.57f,0.58f,0.59f,0.60f,0.60f,0.59f,0.58f,0.57f,0.56f,0.55f},
        ["Mango"]          = new[] {0.50f,0.48f,0.44f,0.40f,0.38f,0.45f,0.55f,0.63f,0.65f,0.60f,0.55f,0.52f},
        ["Turmeric"]       = new[] {0.12f,0.20f,0.35f,0.50f,0.65f,0.72f,0.75f,0.73f,0.68f,0.55f,0.38f,0.20f},
        ["Kokum(Ratamba)"] = new[] {0.52f,0.54f,0.55f,0.56f,0.57f,0.58f,0.57f,0.55f,0.53f,0.52f,0.51f,0.51f},
        ["Pepper"]         = new[] {0.55f,0.57f,0.60f,0.62f,0.65f,0.66f,0.65f,0.62f,0.58f,0.55f,0.53f,0.52f},
        ["Banana"]         = new[] {0.45f,0.52f,0.60f,0.68f,0.74f,0.76f,0.75f,0.70f,0.62f,0.52f,0.45f,0.42f},
        // Legacy keys kept for backwards compatibility
        ["Rice"]           = new[] {0.12f,0.18f,0.30f,0.48f,0.62f,0.72f,0.78f,0.80f,0.78f,0.70f,0.55f,0.38f},
        ["Wheat"]          = new[] {0.10f,0.16f,0.28f,0.45f,0.60f,0.70f,0.76f,0.79f,0.76f,0.68f,0.52f,0.35f},
        ["Soybean"]        = new[] {0.14f,0.22f,0.38f,0.55f,0.70f,0.80f,0.84f,0.82f,0.74f,0.62f,0.45f,0.28f},
        ["Sugarcane"]      = new[] {0.10f,0.14f,0.22f,0.35f,0.50f,0.62f,0.70f,0.76f,0.80f,0.82f,0.80f,0.76f},
    };

    // Model uncertainty (MAE, q/ha)
    private static readonly Dictionary<string, float> CropMae = new()
    {
        ["Paddy(Deshaj)"]  = 2.1f,
        ["Coconut"]        = 4.5f,
        ["Cashewnuts"]     = 1.2f,
        ["Arecanut"]       = 2.5f,
        ["Mango"]          = 8.0f,
        ["Turmeric"]       = 5.0f,
        ["Kokum(Ratamba)"] = 3.0f,
        ["Pepper"]         = 0.5f,
        ["Banana"]         = 18.0f,
        ["Rice"]           = 1.83f,
        ["Wheat"]          = 2.05f,
        ["Soybean"]        = 1.47f,
        ["Sugarcane"]      = 3.21f,
    };

    // Fallback yield estimates (q/ha) — Sindhudurg district averages
    private static readonly Dictionary<string, float> FallbackYield = new()
    {
        ["Paddy(Deshaj)"]  = 22.0f,
        ["Coconut"]        = 40.0f,   // copra equivalent
        ["Cashewnuts"]     = 10.0f,   // raw nut
        ["Arecanut"]       = 20.0f,   // dry nut
        ["Mango"]          = 85.0f,
        ["Turmeric"]       = 45.0f,   // dry turmeric
        ["Kokum(Ratamba)"] = 25.0f,
        ["Pepper"]         = 4.5f,    // dry pepper
        ["Banana"]         = 200.0f,
        ["Rice"]           = 18.4f,
        ["Wheat"]          = 18.4f,
        ["Soybean"]        = 12.0f,
        ["Sugarcane"]      = 650.0f,
    };

    // Maturity days (from planting/sowing to harvest)
    private static readonly Dictionary<string, int> MaturityDays = new()
    {
        ["Paddy(Deshaj)"]  = 130,
        ["Coconut"]        = 365,
        ["Cashewnuts"]     = 270,
        ["Arecanut"]       = 365,
        ["Mango"]          = 120,  // from flowering
        ["Turmeric"]       = 240,
        ["Kokum(Ratamba)"] = 120,  // from flowering
        ["Pepper"]         = 180,
        ["Banana"]         = 300,
        ["Rice"]           = 120,
        ["Wheat"]          = 135,
        ["Soybean"]        = 95,
        ["Sugarcane"]      = 365,
    };

    // MSP / market reference prices (₹/quintal, 2024)
    private static readonly Dictionary<string, int> MarketPrice = new()
    {
        ["Paddy(Deshaj)"]  = 2183,   // MSP 2024
        ["Coconut"]        = 3200,   // copra MSP
        ["Cashewnuts"]     = 6000,
        ["Arecanut"]       = 35000,  // market rate
        ["Mango"]          = 3000,
        ["Turmeric"]       = 7500,   // MSP 2024
        ["Kokum(Ratamba)"] = 8000,
        ["Pepper"]         = 65000,
        ["Banana"]         = 1200,
        ["Rice"]           = 2183,
        ["Wheat"]          = 2275,
        ["Soybean"]        = 4600,
        ["Sugarcane"]      = 315,
    };

    public async Task<PredictionResult?> PredictAsync(string cropType,
                                                        float[]? iotFeatures,
                                                        string? plantingDate,
                                                        string? season = null,
                                                        string? irrigationType = null)
    {
        if (_session is null)
            await LoadModelAsync();

        if (_session is null)
            return FallbackPrediction(cropType, iotFeatures, plantingDate, irrigationType);

        try
        {
            var sat        = BuildSatelliteTensor(cropType);
            var (nodes, ei, ea) = BuildGraph(iotFeatures);

            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("satellite",   sat),
                NamedOnnxValue.CreateFromTensor("graph_nodes", nodes),
                NamedOnnxValue.CreateFromTensor("edge_index",  ei),
                NamedOnnxValue.CreateFromTensor("edge_attr",   ea),
            };

            var t0 = DateTime.UtcNow;
            using var results = _session.Run(inputs);
            int latency = (int)(DateTime.UtcNow - t0).TotalMilliseconds;

            float yield = Math.Max(0.1f, results[0].AsEnumerable<float>().First());

            return new PredictionResult
            {
                PredictedYield     = MathF.Round(yield, 2),
                UncertaintyBand    = CropMae.GetValueOrDefault(cropType, 2.0f),
                ModelVersion       = "agrimitra_int8_v1.0",
                InferenceLatencyMs = latency,
                IsOffline          = true,
                CropType           = cropType,
                FertilizerAdvisory = GenerateFertilizerAdvisory(cropType, iotFeatures),
                IrrigationAdvisory = GenerateIrrigationAdvisory(iotFeatures, irrigationType),
                MarketAdvisory     = GenerateMarketAdvisory(cropType, plantingDate, yield),
            };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"On-device inference error: {ex.Message}");
            return FallbackPrediction(cropType, iotFeatures, plantingDate, irrigationType);
        }
    }

    private async Task LoadModelAsync()
    {
        try
        {
            var modelPath = Path.Combine(FileSystem.AppDataDirectory, _modelFileName);
            if (!File.Exists(modelPath))
            {
                using var stream = await FileSystem.OpenAppPackageFileAsync(_modelFileName);
                using var file   = File.Create(modelPath);
                await stream.CopyToAsync(file);
            }

            var opts = new SessionOptions();
            opts.ExecutionMode          = ExecutionMode.ORT_SEQUENTIAL;
            opts.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL;
            _session = new InferenceSession(modelPath, opts);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Model load error: {ex.Message}");
            _session = null;
        }
    }

    private DenseTensor<float> BuildSatelliteTensor(string cropType)
    {
        var curve = NdviCurves.GetValueOrDefault(cropType,
                        NdviCurves["Paddy(Deshaj)"]);
        var data  = new float[1 * N_CHANNELS * N_WEEKS * IMG_H * IMG_W];
        var rng   = new Random(42);

        for (int t = 0; t < N_WEEKS; t++)
        {
            float ndvi = curve[t];
            for (int h = 0; h < IMG_H; h++)
            for (int w = 0; w < IMG_W; w++)
            {
                int baseIdx = (t * IMG_H * IMG_W) + (h * IMG_W) + w;
                data[0 * N_WEEKS * IMG_H * IMG_W + baseIdx] = 0.25f - 0.18f * ndvi;  // Red
                data[1 * N_WEEKS * IMG_H * IMG_W + baseIdx] = 0.08f + 0.10f * ndvi;  // Green
                data[2 * N_WEEKS * IMG_H * IMG_W + baseIdx] = 0.08f + 0.62f * ndvi;  // NIR
                data[3 * N_WEEKS * IMG_H * IMG_W + baseIdx] = 0.22f - 0.08f * ndvi;  // SWIR
            }
        }
        return new DenseTensor<float>(data, new[] { 1, N_CHANNELS, N_WEEKS, IMG_H, IMG_W });
    }

    private (DenseTensor<float> nodes, DenseTensor<long> edgeIndex, DenseTensor<float> edgeAttr)
        BuildGraph(float[]? iotFeatures)
    {
        float[] n0 = iotFeatures?.Length >= NODE_FEAT
            ? iotFeatures[..NODE_FEAT]
            : new float[] { 100f, 50f, 200f, 35f, 6.5f, 28f, 0.6f, 100f };

        var nodeData = new float[GRAPH_NODES * NODE_FEAT];
        var rng = new Random(123);
        for (int n = 0; n < GRAPH_NODES; n++)
        for (int f = 0; f < NODE_FEAT; f++)
            nodeData[n * NODE_FEAT + f] = n == 0 ? n0[f] : n0[f] * (0.8f + (float)rng.NextDouble() * 0.4f);

        var edges = new List<(int s, int d)>();
        for (int i = 0; i < GRAPH_NODES; i++)
        for (int j = 0; j < GRAPH_NODES; j++)
            if (i != j) edges.Add((i, j));

        var eiData = new long[2 * edges.Count];
        var eaData = new float[edges.Count];
        for (int i = 0; i < edges.Count; i++)
        {
            eiData[i]               = edges[i].s;
            eiData[edges.Count + i] = edges[i].d;
            eaData[i]               = 1f;
        }

        return (
            new DenseTensor<float>(nodeData, new[] { GRAPH_NODES, NODE_FEAT }),
            new DenseTensor<long> (eiData,   new[] { 2, edges.Count }),
            new DenseTensor<float>(eaData,   new[] { edges.Count, 1 })
        );
    }

    // ── Advisory helpers ──────────────────────────────────────────────────────

    private static string GenerateFertilizerAdvisory(string crop, float[]? iot)
    {
        float n = iot?.Length > 0 ? iot[0] : 100f;
        float p = iot?.Length > 1 ? iot[1] : 50f;
        float k = iot?.Length > 2 ? iot[2] : 200f;
        return crop switch
        {
            "Paddy(Deshaj)" or "Rice" =>
                n < 80 ? "Apply 60 kg/ha Urea in 2 splits (basal + tillering)"
                       : p < 30 ? "Apply 40 kg/ha SSP at basal dose"
                                : "Apply 50 kg/ha Urea at top-dressing stage",
            "Coconut" =>
                "Apply 500g N + 320g P₂O₅ + 1200g K₂O per palm/year in 2 splits (Jun & Dec). " +
                "Add 50 kg FYM/palm annually.",
            "Cashewnuts" =>
                n < 60 ? "Apply 700g N + 175g P₂O₅ + 680g K₂O per tree (split May & Oct)"
                       : "Apply 340g K₂O per tree before flowering to improve nut set",
            "Arecanut" =>
                "Apply 100g N + 40g P₂O₅ + 140g K₂O per palm/year in 3 splits. Add 25 kg FYM.",
            "Mango" =>
                p < 30 ? "Apply 500g SSP + 400g MOP per tree before flowering"
                       : "Apply 1 kg Urea per tree in Sep–Oct to boost flowering",
            "Turmeric" =>
                n < 80 ? "Apply 60 kg/ha Urea split in 3 doses (0, 45, 90 days after planting)"
                       : k < 100 ? "Apply 50 kg/ha MOP to improve rhizome quality"
                                 : "Apply 40 kg/ha Urea at 45-day stage",
            "Kokum(Ratamba)" =>
                "Apply 200g N + 100g P₂O₅ + 200g K₂O per tree in 2 splits (Jun & Nov).",
            "Pepper" =>
                "Apply 50g N + 50g P₂O₅ + 150g K₂O per vine in 3 splits. " +
                "Add 5 kg compost/vine at onset of monsoon.",
            "Banana" =>
                n < 80 ? "Apply 200g N per plant in 4–5 split doses over crop duration"
                       : "Apply 60g P₂O₅ + 300g K₂O per plant at 3 and 5 months",
            "Wheat" =>
                n < 80 ? "Apply 60 kg/ha Urea split in 3 doses" : "Apply 40 kg/ha Urea at CRI stage",
            "Soybean"   => "Apply rhizobium seed treatment + 20 kg/ha starter N",
            "Sugarcane" => "Apply 120 kg/ha Urea in 3 splits (0, 60, 120 days)",
            _           => "Follow Karnataka/Maharashtra state agriculture department guidelines",
        };
    }

    private static string GenerateIrrigationAdvisory(float[]? iot, string? irrigationType)
    {
        float moisture = iot?.Length > 3 ? iot[3] : 35f;
        string urgency = moisture < 20
            ? $"Soil moisture critically low ({moisture:F0}%). Irrigate 30–40 mm immediately."
            : moisture < 30
                ? $"Soil moisture low ({moisture:F0}%). Schedule irrigation within 48 hours."
                : $"Soil moisture adequate ({moisture:F0}%). Next irrigation in 7–10 days.";

        string method = irrigationType switch
        {
            "Drip / Sprinkler" => " Use drip at 4–6 L/hr per emitter for efficient use.",
            "Irrigated (Canal/Well)" => " Use basin irrigation; ensure good drainage for coastal soils.",
            _ => " Sindhudurg receives 3500–4000 mm rain; supplement only in dry spells.",
        };

        return urgency + method;
    }

    private static string GenerateMarketAdvisory(string crop, string? plantingDate, float yieldPred)
    {
        DateTime plant   = DateTime.TryParse(plantingDate, out var d) ? d : DateTime.Today;
        DateTime harvest = plant.AddDays(MaturityDays.GetValueOrDefault(crop, 120));
        DateTime sellStart = harvest.AddDays(10);
        DateTime sellEnd   = sellStart.AddDays(20);
        int price = MarketPrice.GetValueOrDefault(crop, 2000);

        string mandiNote = crop switch
        {
            "Paddy(Deshaj)"  => "Sell at Sindhudurg APMC or RPC (Rice Procurement Centre).",
            "Cashewnuts"     => "Contact Konkan Cashew processors or Maharashtra Cashew Board.",
            "Arecanut"       => "Nearest APMC: Sawantwadi or Kudal. Check Maharashtra APMC e-market.",
            "Mango"          => "Book Hafus/Alphonso export early (Mar–Apr). Contact Konkan Mango Growers.",
            "Turmeric"       => "Sell at Sangli APMC (largest turmeric market) or local APMCs.",
            "Kokum(Ratamba)" => "Contact Sindhudurg Kokum producers cooperative or agro-processing units.",
            "Pepper"         => "Sell to Spices Board certified buyers or Cochin market agents.",
            "Coconut"        => "Coconut Development Board procurement centers; copra MSP ₹3200/q.",
            _                => "Contact nearest APMC mandi 1 week before harvest.",
        };

        return $"Est. harvest: {harvest:MMM dd, yyyy}. " +
               $"Best selling window: {sellStart:MMM dd} – {sellEnd:MMM dd, yyyy}. " +
               $"Ref. price: ₹{price}/quintal. " +
               $"Est. revenue: ₹{(int)(yieldPred * price):N0}. " +
               mandiNote;
    }

    private static PredictionResult FallbackPrediction(string cropType,
                                                         float[]? iot,
                                                         string? plantingDate,
                                                         string? irrigationType)
    {
        float yield = FallbackYield.GetValueOrDefault(cropType, 15.0f);
        return new PredictionResult
        {
            PredictedYield     = yield,
            UncertaintyBand    = CropMae.GetValueOrDefault(cropType, 2.0f),
            ModelVersion       = "fallback_v1.0",
            InferenceLatencyMs = 0,
            IsOffline          = true,
            CropType           = cropType,
            FertilizerAdvisory = GenerateFertilizerAdvisory(cropType, iot),
            IrrigationAdvisory = GenerateIrrigationAdvisory(iot, irrigationType),
            MarketAdvisory     = GenerateMarketAdvisory(cropType, plantingDate, yield),
        };
    }

    public void Dispose() => _session?.Dispose();
}

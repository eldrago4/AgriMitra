# AgriMitra — App Description

## Purpose

AgriMitra is an AI-powered crop yield prediction and farmer advisory mobile application designed for Indian smallholder farmers. It addresses two systemic problems in rural agriculture:

- **The Precision Void** — farmers operate without localized, real-time field data, causing suboptimal resource use (fertilizers, water, seeds).
- **The MSP Paradox** — farmers sell produce at below-market prices because they lack yield forecasts and market timing intelligence.

AgriMitra delivers village-level yield predictions and actionable advisories directly to a farmer's phone — including feature phones via SMS/IVR — without requiring internet connectivity at inference time.

---

## Introduction

Indian agriculture sustains over 150 million livelihoods but incurs an estimated ₹1.5 lakh crore in annual losses due to climate volatility and information asymmetry. Smallholder farmers, who form the backbone of food production, make decisions without access to localized crop forecasts or market insights.

AgriMitra bridges this gap using a multi-modal AI pipeline that fuses:
- **ISRO satellite imagery** (spatiotemporal crop growth patterns via Swin3D Transformers)
- **IoT sensor data** (soil NPK, moisture, pH, temperature via Graph Neural Networks)
- **Bhuvan NDVI API** (vegetation health indices)

The resulting yield prediction model is compressed for on-device inference (28.6 MB, 310 ms latency on a mid-range Android phone) and surfaced through a .NET MAUI cross-platform mobile app with SMS/IVR fallback for farmers without smartphones.

The system achieved **R² = 0.883** and **MAPE = 6.9%** across four major crops in Maharashtra (2022–2024), a **15.2% improvement in MAPE** over the best single-modal baseline.

---

## Requirements

### Functional Requirements
- Farm boundary selection via interactive satellite map
- Crop type and planting quantity input
- Optional IoT sensor data submission for higher accuracy
- On-device yield prediction (offline-capable)
- Display of predicted yield in quintals per hectare (q/ha)
- Input use advisories (fertilizer, water recommendations)
- MSP market timing advisories
- SMS and IVR advisory delivery for feature phone users

### Non-Functional Requirements
- Inference latency ≤ 500 ms on mid-range Android hardware
- Model size ≤ 30 MB on-device
- Works offline after initial model download
- Supports Android and iOS from a single codebase
- Accessible to low-digital-literacy users (voice IVR fallback)

### Hardware Requirements (User Device)
| Tier | Minimum Spec | Notes |
|---|---|---|
| Target Android | Qualcomm Snapdragon 665, 4 GB RAM, Android 12 | Full app with on-device inference |
| Low-end Android | 2 GB RAM, Android 9+ | Server-side inference mode |
| Feature phone | Any phone with SMS/call capability | Advisory delivered via SMS or IVR call |

### Software / Tech Stack
| Layer | Technology |
|---|---|
| Mobile App | .NET MAUI 9.0 (C#, Android + iOS) |
| On-device Inference | ONNX Runtime Mobile 1.17 |
| Server API | ASP.NET Core 8.0 |
| AI Model Training | PyTorch 2.2, PyTorch Geometric 2.5 |
| Satellite Data | ISRO Cartosat-3 / Resourcesat-2 (5 m resolution) |
| Vegetation Index | ISRO Bhuvan NDVI API |
| Model Export | ONNX opset 17, INT8 dynamic quantization |
| Mapping | Microsoft.Maui.Maps |
| SMS/IVR | Third-party SMS gateway + IVR provider |

### Backend Infrastructure (Server-Side)
- **Training:** Google Colab Pro, NVIDIA Tesla T4 GPU (16 GB VRAM)
- **Serving:** AWS EC2 c5.2xlarge (8 vCPUs, 16 GB RAM), Ubuntu 22.04

---

## Required UI

### Screen 1 — Onboarding / Registration
- Farmer name and phone number entry
- Village/district selection (dropdown or map pin)
- Language selection (Marathi, Hindi, English)
- Optional: IoT device ID pairing

### Screen 2 — Home Dashboard
- Greeting with farmer name
- Quick-action buttons: **New Prediction**, **My Fields**, **Advisories**, **Market Prices**
- Summary card showing last prediction result (crop name, predicted yield, date)
- Notification area for new advisories or alerts

### Screen 3 — Farm Selection (Map)
- Full-screen satellite map (Google Maps / Bhuvan basemap)
- Draw farm boundary tool: tap vertices to draw polygon over field
- Polygon highlights selected area with acreage calculation
- "Confirm Field" button once polygon is closed
- Option to load a previously saved field

### Screen 4 — Crop Details Input
- Crop type selector: Rice, Wheat, Soybean, Sugarcane (expandable)
- Expected planting date (date picker)
- Approximate farm area (auto-filled from polygon, editable)
- Optional: IoT sensor data toggle
  - If enabled: soil N, P, K (numeric inputs), moisture (%), pH, temperature (°C)

### Screen 5 — Prediction Loading
- Animated progress indicator
- Status messages: "Fetching satellite data…", "Analyzing vegetation patterns…", "Running AI model…"
- Estimated time display

### Screen 6 — Prediction Result
- **Primary card:** Predicted yield (e.g., *"18.4 quintals/hectare"*)
- Confidence range (e.g., ±1.8 q/ha)
- Crop name and field area
- Visual yield-vs-regional-average comparison bar
- Advisory cards (scrollable):
  - Fertilizer recommendation (NPK ratios, timing)
  - Irrigation schedule advisory
  - MSP market timing recommendation (e.g., "Best selling window: Oct 15–Nov 10")
- "Save to My Fields" button
- "Share via SMS" button (sends summary to registered number)

### Screen 7 — My Fields
- List of saved farm polygons with last prediction, crop, and date
- Tap to re-run prediction on existing field
- Delete / rename field options

### Screen 8 — Market Insights
- Crop-wise MSP table (government prices)
- Historical price trend mini-chart
- Recommended selling window based on predicted yield and seasonal patterns
- Nearest APMC mandi distance and contact info

### Screen 9 — Settings
- Language preference
- SMS notification opt-in/out
- IoT device management
- Model update check (download latest ONNX model)
- About / Help

---

## App Flow

```
[Launch]
    │
    ▼
[Onboarding/Login]  ──► (first time: registration)
    │
    ▼
[Home Dashboard]
    │
    ├──► [My Fields] ──► (select saved field) ──► [Prediction Result]
    │
    ├──► [New Prediction]
    │         │
    │         ▼
    │    [Farm Selection — Map]
    │         │  (draw polygon)
    │         ▼
    │    [Crop Details Input]
    │         │  (crop type, IoT data optional)
    │         ▼
    │    [Prediction Loading]
    │         │  (fetch NDVI → build graph → run ONNX model)
    │         ▼
    │    [Prediction Result]
    │         │
    │         ├──► [Save Field]
    │         └──► [Share SMS / IVR]
    │
    ├──► [Market Insights]
    │
    └──► [Settings]
```

**Offline flow:** If no internet, the app uses the last-cached satellite/NDVI tile and runs inference entirely on-device via ONNX Runtime Mobile. A banner indicates cached data age.

**Feature phone flow:** Farmer calls the IVR number → voice prompt for village + crop → system runs server-side inference → reads out predicted yield and key advisory in local language.

---

## Features (Detailed)

### 1. Interactive Farm Boundary Drawing
The farmer opens a satellite-backed map and draws a polygon over their field by tapping boundary vertices. The app calculates the enclosed area in hectares and saves the polygon coordinates (GeoJSON). These coordinates are the entry point for all subsequent data fetching — satellite tile selection, NDVI time-series query, and graph-node geolocation.

### 2. Multi-Modal AI Yield Prediction
The prediction engine fuses two specialized AI models:

**Swin3D Transformer (Temporal / Satellite branch)**
- Processes a 12-week stack of ISRO Cartosat-3/Resourcesat-2 satellite imagery (224×224 pixels, 4 spectral channels: Red, Green, NIR, SWIR) for the selected farm polygon.
- Treats the crop season as a 3D spatiotemporal volume and extracts a 256-dimensional feature vector encoding growth trajectory.
- Pre-trained on Kinetics-400 video data and fine-tuned for agricultural spatiotemporal patterns.
- Cloud-cover gaps are handled by temporal median imputation.

**GATv2 Graph Neural Network (Spatial / IoT branch)**
- Models the farm field and its neighbors (within 5 km radius) as a graph where each node carries 8 features: soil N, P, K, moisture, pH, temperature, NDVI, and elevation.
- Edge weights encode inverse Euclidean distance between farm centroids.
- Two-layer GATv2 convolution with 4 attention heads produces a 128-dimensional spatial feature vector capturing inter-field dependencies.

**Fusion**
- The 256-dim (Swin3D) and 128-dim (GNN) vectors are concatenated into a 384-dim fused representation.
- A 3-layer MLP regression head produces the final yield estimate in quintals per hectare.

### 3. On-Device Edge Inference
- The trained PyTorch model is exported to ONNX and dynamically quantized to INT8.
- Size reduced from 112.4 MB (FP32) to **28.6 MB** (INT8) — a 3.93× compression.
- Inference latency: **310 ms** on Snapdragon 665 (single-threaded).
- RAM usage: **95 MB** — suitable for devices with 2 GB+ RAM.
- Accuracy penalty: only −0.7% in R² (0.883 → 0.876).
- The ONNX model is bundled in the app and can be updated OTA.

### 4. Actionable Precision Advisories
Based on the yield prediction and input data, the app generates:
- **Fertilizer advisory:** Recommended NPK ratios and application timing for the specific crop and soil condition.
- **Irrigation schedule:** Water requirement estimate for the predicted growth trajectory.
- **Input waste reduction:** Flags over-application risk when sensor data indicates high existing nutrient levels.

### 5. MSP Market Timing Advisory
- Uses predicted yield volume + government MSP data + historical mandi price trends to recommend the optimal post-harvest selling window.
- Helps farmers avoid the MSP Paradox by timing market entry when prices are above the floor price.
- Nearest APMC mandi details included.

### 6. Offline-First Architecture
- Once the ONNX model is downloaded, all inference runs entirely on-device.
- Satellite tiles and NDVI data are cached on last sync; staleness is indicated to the user.
- Advisory generation, field history, and results browsing all work without connectivity.

### 7. SMS / IVR Fallback for Feature Phones
- Farmers without smartphones can register a phone number.
- An outbound IVR call (or inbound toll-free call) walks the farmer through crop and location selection via keypad input.
- The server runs inference and reads the yield estimate and top advisory aloud in Marathi, Hindi, or English.
- An SMS summary is sent after the call.

### 8. Multi-Crop Support
Supported crop types (with validated performance):
| Crop | Best Season | R² | MAPE |
|---|---|---|---|
| Rice | Kharif | 0.891 | 7.2% |
| Wheat | Rabi | 0.874 | 8.1% |
| Soybean | Kharif | 0.906 | 6.4% |
| Sugarcane | Annual | 0.861 | 5.9% |

### 9. Field History and Progress Tracking
- Each saved field retains a prediction history (date, crop, predicted yield, actual yield if entered by farmer).
- Farmers can optionally record actual harvest yield to improve local calibration over time.

### 10. Regional Language Support
- UI available in Marathi, Hindi, and English.
- IVR voice prompts and SMS texts delivered in the farmer's preferred language.

---

## Outputs

### Primary Output — Yield Prediction
- **Value:** Predicted crop yield in **quintals per hectare (q/ha)**
- **Confidence range:** ±MAE band (e.g., ±1.83 q/ha for rice)
- **Context:** Shown alongside district/regional average yield for comparison

Example display:
```
Predicted Yield: 18.4 q/ha
Range: 16.6 – 20.2 q/ha
District Average (Rice, Kolhapur): 16.1 q/ha
Your field is estimated to be above average.
```

### Secondary Outputs — Advisories
1. **Fertilizer Card**
   - Recommended NPK (kg/ha), application stage, and timing
   - Warning if soil sensors indicate nutrient excess

2. **Irrigation Card**
   - Estimated water requirement (mm/week) for remaining growth period
   - Advisory on deficit or surplus irrigation risk

3. **Market Timing Card**
   - Recommended selling window (date range)
   - Current MSP vs. expected mandi price trend
   - Nearest APMC mandi name, distance, contact

### Tertiary Outputs — Alerts and Notifications
- Push notification when a new seasonal advisory is generated
- SMS advisory summary after IVR interaction
- In-app alert when satellite data for the field has been refreshed

### System-Level Performance (what the AI delivers)
| Metric | Value |
|---|---|
| Overall R² | 0.883 |
| Overall MAPE | 6.9% |
| On-device inference latency | 310 ms |
| Model size (deployed) | 28.6 MB |
| MAPE improvement over ARIMA baseline | 62% |
| MAPE improvement over best single-modal | 15.2% |

---

## Future Features (Planned)
- **Crop stress early warning:** Classification branch for drought, pest, and flood stress detection days before visible damage.
- **SAR data integration:** ISRO RISAT-1A Synthetic Aperture Radar imagery to fill cloud-cover gaps during monsoon.
- **Federated learning:** Model improvement from distributed farmer data without centralizing records.
- **Multi-state expansion:** Coverage beyond Maharashtra to Punjab, Andhra Pradesh, Karnataka.
- **Smaller backbone:** MobileNet-based 3D backbone to reduce model size below 10 MB for entry-level devices.

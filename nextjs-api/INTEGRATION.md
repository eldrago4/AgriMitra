# AgriMitra — Next.js API Integration Guide

This folder contains ready-to-copy API routes for your existing Vercel Next.js project.

## Step 1 — Copy API route files

From the `nextjs-api/` folder, copy:

```
app/api/predict/route.ts      → <your-nextjs-project>/app/api/predict/route.ts
app/api/farmers/route.ts      → <your-nextjs-project>/app/api/farmers/route.ts
app/api/fields/route.ts       → <your-nextjs-project>/app/api/fields/route.ts
app/api/market/msp/route.ts   → <your-nextjs-project>/app/api/market/msp/route.ts
app/api/market/mandi/route.ts → <your-nextjs-project>/app/api/market/mandi/route.ts
prisma/schema.prisma          → <your-nextjs-project>/prisma/schema.prisma
```

## Step 2 — Install Prisma

In your Next.js project:

```bash
npm install @prisma/client prisma
npx prisma generate
```

## Step 3 — Set up Vercel Postgres

1. In the Vercel dashboard → Storage → Create → Postgres database
2. Copy the `DATABASE_URL` connection string

## Step 4 — Push the schema

```bash
DATABASE_URL="your_connection_string" npx prisma db push
```

## Step 5 — Deploy the inference service to 1ved.cloud

```bash
# On your 1ved.cloud server:
git clone ... agrimitra-infer
cd agrimitra-infer/deploy
cp /path/to/agrimitra_int8.onnx .
docker compose up -d --build

# Verify:
curl https://1ved.cloud/health
```

## Step 6 — Set Vercel environment variables

In the Vercel dashboard → Settings → Environment Variables:

| Variable           | Value                                                      |
|--------------------|------------------------------------------------------------|
| `DATABASE_URL`     | Your Vercel Postgres connection URL                        |
| `INFER_URL`        | `https://1ved.cloud`                                       |
| `BHOONIDHI_USER`   | `ved4`                                                     |
| `BHOONIDHI_PASS`   | Your NRSC Bhoonidhi account password                       |
| `BHOONIDHI_API`    | `https://bhoonidhi.nrsc.gov.in/bhoonidhi-api`              |
| `DATA_GOV_KEY`     | `579b464db66ec23bdd0000019c2c6fd04bc94be57c33063c3c1baf4a` |

> **Important — Bhoonidhi whitelist**: NRSC has whitelisted the 1ved.cloud server IP.
> All Bhoonidhi API calls MUST route through `https://1ved.cloud/api/bhoonidhi/*`.
> Do **not** store `BHOONIDHI_USER`/`BHOONIDHI_PASS` in local `.env` files or commit
> them to source control.  They belong only in Vercel's encrypted env var store.

## Step 7 — Deploy

```bash
git add .
git commit -m "Add AgriMitra API routes"
git push   # Vercel auto-deploys
```

## Step 8 — Test the endpoints

```bash
# Health check on inference service
curl https://1ved.cloud/health

# Test prediction endpoint
curl -X POST https://<your-vercel-site>/api/predict \
  -H "Content-Type: application/json" \
  -d '{
    "farmCoordinates": [[16.55, 73.70], [16.56, 73.70], [16.56, 73.71], [16.55, 73.71]],
    "cropType": "Wheat",
    "iotSensorData": {
      "soilN": 45, "soilP": 28, "soilK": 120,
      "moisture": 18, "ph": 6.5, "temperature": 28,
      "ndvi": 0.65, "elevation": 120
    },
    "plantingDate": "2023-10-15"
  }'
```

Expected response:
```json
{
  "predictedYield": 18.4,
  "uncertaintyBand": 2.05,
  "modelVersion": "agrimitra_int8_v1.0",
  "inferenceLatencyMs": 347,
  "fertilizerAdvisory": "...",
  "irrigationAdvisory": "...",
  "marketAdvisory": "Best selling window: Apr 15 – May 10, 2024..."
}
```

## Step 9 — Update the mobile app API base URL

In `mobile/AgriMitraMobile/Services/ApiService.cs`, update:

```csharp
private const string BaseUrl = "https://<your-vercel-site>";
```

## API Reference

| Method | Endpoint              | Body / Params                  | Returns                  |
|--------|-----------------------|-------------------------------|--------------------------|
| POST   | `/api/predict`        | `{farmCoordinates, cropType, iotSensorData?, plantingDate?, fieldId?}` | Prediction result |
| POST   | `/api/farmers`        | `{name, phone, village, district, language}` | Farmer object |
| GET    | `/api/farmers`        | —                             | List of farmers          |
| POST   | `/api/fields`         | `{farmerId, polygonGeoJson, areaHectares, label}` | FarmField object |
| GET    | `/api/fields`         | `?farmerId=xxx`               | List of fields           |
| GET    | `/api/market/msp`     | —                             | MSP 2023-24 table        |
| GET    | `/api/market/mandi`   | `?lat=16.5&lon=73.7`          | Nearest APMC mandi       |

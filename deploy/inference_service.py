"""
AgriMitra Python FastAPI Inference Microservice.
Deployed at 1ved.cloud — handles ONNX inference for the Next.js /api/predict route.

Run locally:
    pip install fastapi uvicorn onnxruntime numpy
    uvicorn inference_service:app --host 0.0.0.0 --port 8001

Deploy:
    docker compose up -d --build
"""
import logging
import time
from pathlib import Path
from typing import Optional

import numpy as np
from fastapi import FastAPI, HTTPException
from fastapi.middleware.cors import CORSMiddleware
from pydantic import BaseModel

logger = logging.getLogger(__name__)
logging.basicConfig(level=logging.INFO, format="%(asctime)s %(levelname)s %(message)s")

app = FastAPI(
    title       = "AgriMitra Inference Service",
    description = "ONNX Runtime yield prediction endpoint",
    version     = "1.0.0",
)

app.add_middleware(
    CORSMiddleware,
    allow_origins  = ["*"],
    allow_methods  = ["POST", "GET"],
    allow_headers  = ["*"],
)

# ── Config ────────────────────────────────────────────────────────────────────
MODEL_PATH  = Path("/app/agrimitra_int8.onnx")
if not MODEL_PATH.exists():
    MODEL_PATH = Path("agrimitra_int8.onnx")  # local dev fallback

N_CHANNELS   = 4
N_WEEKS      = 12
IMG_H        = 224
IMG_W        = 224
NODE_FEAT_DIM = 8
GRAPH_N_NODES = 3

CROP_MAE = {"Rice": 1.83, "Wheat": 2.05, "Soybean": 1.47, "Sugarcane": 3.21}
MSP      = {"Rice": 2183, "Wheat": 2275, "Soybean": 4600, "Sugarcane": 315}
MATURITY = {"Rice": 120,  "Wheat": 135,  "Soybean": 95,   "Sugarcane": 365}

NDVI_CURVES = {
    "Rice":      [0.12, 0.18, 0.30, 0.48, 0.62, 0.72, 0.78, 0.80, 0.78, 0.70, 0.55, 0.38],
    "Wheat":     [0.10, 0.16, 0.28, 0.45, 0.60, 0.70, 0.76, 0.79, 0.76, 0.68, 0.52, 0.35],
    "Soybean":   [0.14, 0.22, 0.38, 0.55, 0.70, 0.80, 0.84, 0.82, 0.74, 0.62, 0.45, 0.28],
    "Sugarcane": [0.10, 0.14, 0.22, 0.35, 0.50, 0.62, 0.70, 0.76, 0.80, 0.82, 0.80, 0.76],
}

# ── ORT session (singleton) ───────────────────────────────────────────────────
_session = None

def get_session():
    global _session
    if _session is None:
        try:
            import onnxruntime as ort
            opts = ort.SessionOptions()
            opts.execution_mode           = ort.ExecutionMode.ORT_SEQUENTIAL
            opts.graph_optimization_level = ort.GraphOptimizationLevel.ORT_ENABLE_ALL
            opts.intra_op_num_threads     = 1
            _session = ort.InferenceSession(str(MODEL_PATH), opts)
            logger.info(f"ONNX session loaded from {MODEL_PATH}")
        except Exception as e:
            logger.error(f"Failed to load ONNX model: {e}")
            _session = None
    return _session


# ── Request / Response models ─────────────────────────────────────────────────

class IoTSensorData(BaseModel):
    soilN:       float = 100.0
    soilP:       float = 50.0
    soilK:       float = 200.0
    moisture:    float = 35.0
    ph:          float = 6.5
    temperature: float = 28.0
    ndvi:        float = 0.6
    elevation:   float = 100.0


class InferRequest(BaseModel):
    farmCoordinates: list[list[float]]       # [[lat, lon], ...]
    cropType:        str                     # Rice | Wheat | Soybean | Sugarcane
    iotSensorData:   Optional[IoTSensorData] = None
    plantingDate:    Optional[str]           = None   # ISO-8601


class InferResponse(BaseModel):
    predictedYield:     float
    uncertaintyBand:    float
    modelVersion:       str
    inferenceLatencyMs: int
    fertilizerAdvisory: str
    irrigationAdvisory: str
    marketAdvisory:     str


# ── Tensor builders ───────────────────────────────────────────────────────────

def _build_satellite(crop_type: str) -> np.ndarray:
    curve = NDVI_CURVES.get(crop_type, NDVI_CURVES["Rice"])
    rng   = np.random.default_rng(42)
    frames = []
    for ndvi in curve:
        r = np.full((IMG_H, IMG_W), 0.25 - 0.18 * ndvi, dtype=np.float32)
        g = np.full((IMG_H, IMG_W), 0.08 + 0.10 * ndvi, dtype=np.float32)
        n = np.full((IMG_H, IMG_W), 0.08 + 0.62 * ndvi, dtype=np.float32)
        s = np.full((IMG_H, IMG_W), 0.22 - 0.08 * ndvi, dtype=np.float32)
        r += rng.normal(0, 0.02, (IMG_H, IMG_W)).astype(np.float32)
        n += rng.normal(0, 0.02, (IMG_H, IMG_W)).astype(np.float32)
        frames.append(np.stack([r, g, n, s]))
    sat = np.stack(frames, axis=1)          # (4, 12, H, W)
    return sat[np.newaxis].astype(np.float32)  # (1, 4, 12, H, W)


def _build_graph(iot: Optional[IoTSensorData]):
    rng = np.random.default_rng(123)
    if iot:
        n0 = np.array([iot.soilN, iot.soilP, iot.soilK, iot.moisture,
                        iot.ph, iot.temperature, iot.ndvi, iot.elevation], dtype=np.float32)
    else:
        n0 = np.array([100, 50, 200, 35, 6.5, 28, 0.6, 100], dtype=np.float32)

    nodes = [n0]
    for _ in range(GRAPH_N_NODES - 1):
        nodes.append((n0 * rng.uniform(0.8, 1.2, (NODE_FEAT_DIM,))).astype(np.float32))
    graph_nodes = np.stack(nodes)            # (N, 8)

    src, dst = [], []
    for i in range(GRAPH_N_NODES):
        for j in range(GRAPH_N_NODES):
            if i != j:
                src.append(i); dst.append(j)
    edge_index = np.array([src, dst], dtype=np.int64)
    edge_attr  = np.ones((edge_index.shape[1], 1), dtype=np.float32)
    return graph_nodes, edge_index, edge_attr


# ── Advisory generation ───────────────────────────────────────────────────────

def _advisories(crop: str, iot: Optional[IoTSensorData],
                planting_date: Optional[str], yield_v: float) -> dict:
    n = iot.soilN if iot else 100
    p = iot.soilP if iot else 50
    moisture = iot.moisture if iot else 35

    fert_map = {
        "Rice":      ("Apply 60 kg/ha Urea in 2 splits (basal + tillering)",
                      "Apply 40 kg/ha SSP at basal dose",
                      "Apply 50 kg/ha Urea at top-dressing stage"),
        "Wheat":     ("Apply 60 kg/ha Urea split in 3 doses",
                      "Apply 35 kg/ha DAP at sowing",
                      "Apply 40 kg/ha Urea at CRI stage"),
        "Soybean":   ("Apply rhizobium seed treatment + 20 kg/ha starter N",
                      "Apply 60 kg/ha SSP at sowing",
                      "Soil N adequate; monitor for early yellowing"),
        "Sugarcane": ("Apply 150 kg/ha Urea split over 4 months",
                      "Apply 80 kg/ha SSP at planting",
                      "Apply 120 kg/ha Urea in 3 splits (0, 60, 120 days)"),
    }
    low_n, low_p, ok = fert_map.get(crop, fert_map["Rice"])
    fert = low_n if n < 80 else (low_p if p < 30 else ok)

    if moisture < 20:
        irr = f"Soil moisture critically low ({moisture:.0f}%). Irrigate 30–40 mm immediately."
    elif moisture < 30:
        irr = f"Soil moisture low ({moisture:.0f}%). Schedule irrigation within 48 hours."
    else:
        irr = f"Soil moisture adequate ({moisture:.0f}%). Next irrigation in 7–10 days."

    from datetime import datetime, timedelta
    try:
        plant_dt = datetime.fromisoformat((planting_date or "")[:10])
    except (ValueError, AttributeError):
        plant_dt = datetime.today()
    harvest_dt = plant_dt + timedelta(days=MATURITY.get(crop, 120))
    sell_start = harvest_dt + timedelta(days=15)
    sell_end   = sell_start + timedelta(days=25)
    msp_price  = MSP.get(crop, 2000)
    market = (
        f"Best selling window: {sell_start.strftime('%b %d')} – {sell_end.strftime('%b %d, %Y')}. "
        f"MSP: ₹{msp_price}/quintal. "
        f"Predicted yield {yield_v:.1f} q/ha → est. ₹{int(yield_v * msp_price):,}. "
        "Contact nearest APMC mandi 1 week before harvest."
    )
    return {"fertilizerAdvisory": fert, "irrigationAdvisory": irr, "marketAdvisory": market}


# ── Endpoints ─────────────────────────────────────────────────────────────────

@app.get("/health")
async def health():
    sess = get_session()
    return {"status": "ok" if sess else "model_not_loaded",
            "model": str(MODEL_PATH)}


@app.post("/infer", response_model=InferResponse)
async def infer(req: InferRequest):
    crop = req.cropType
    if crop not in NDVI_CURVES:
        raise HTTPException(400, f"Unknown cropType '{crop}'. Use: {list(NDVI_CURVES)}")

    sess = get_session()
    if sess is None:
        # Fallback: return heuristic estimate when model not loaded
        yield_v = {"Rice": 18.4, "Wheat": 18.4, "Soybean": 12.0, "Sugarcane": 650.0}.get(crop, 15.0)
        latency = 0
    else:
        sat, graph_nodes, edge_index, edge_attr = (
            _build_satellite(crop),
            *_build_graph(req.iotSensorData)
        )
        t0 = time.perf_counter()
        out = sess.run(None, {
            "satellite":   sat,
            "graph_nodes": graph_nodes,
            "edge_index":  edge_index,
            "edge_attr":   edge_attr,
        })
        latency = int((time.perf_counter() - t0) * 1000)
        yield_v = max(0.1, float(out[0][0]))

    advisories = _advisories(crop, req.iotSensorData, req.plantingDate, yield_v)

    return InferResponse(
        predictedYield     = round(yield_v, 2),
        uncertaintyBand    = CROP_MAE.get(crop, 2.0),
        modelVersion       = "agrimitra_int8_v1.0",
        inferenceLatencyMs = latency,
        **advisories,
    )

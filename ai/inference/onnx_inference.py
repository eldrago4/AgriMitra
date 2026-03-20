"""
Python ONNX Runtime inference session.
Used by the deploy/inference_service.py for server-side predictions.
"""
import time
import logging
from pathlib import Path
from typing import Optional

import numpy as np

import sys
sys.path.insert(0, str(Path(__file__).parent.parent))
from config import (
    ONNX_INT8, GRAPH_N_NODES, N_CHANNELS, N_WEEKS, IMG_H, IMG_W,
    NODE_FEAT_DIM, CROP_MAE, MATURITY_DAYS, MSP
)

logger = logging.getLogger(__name__)


# ── NDVI crop lookup tables for demo inference ───────────────────────────────

NDVI_CURVES = {
    "Rice":      [0.12, 0.18, 0.30, 0.48, 0.62, 0.72, 0.78, 0.80, 0.78, 0.70, 0.55, 0.38],
    "Wheat":     [0.10, 0.16, 0.28, 0.45, 0.60, 0.70, 0.76, 0.79, 0.76, 0.68, 0.52, 0.35],
    "Soybean":   [0.14, 0.22, 0.38, 0.55, 0.70, 0.80, 0.84, 0.82, 0.74, 0.62, 0.45, 0.28],
    "Sugarcane": [0.10, 0.14, 0.22, 0.35, 0.50, 0.62, 0.70, 0.76, 0.80, 0.82, 0.80, 0.76],
}


class AgriMitraOnnxSession:
    """Thread-safe ONNX Runtime inference wrapper."""

    def __init__(self, model_path: Path = ONNX_INT8):
        import onnxruntime as ort
        opts = ort.SessionOptions()
        opts.execution_mode          = ort.ExecutionMode.ORT_SEQUENTIAL
        opts.graph_optimization_level = ort.GraphOptimizationLevel.ORT_ENABLE_ALL
        opts.intra_op_num_threads    = 1

        self.session      = ort.InferenceSession(str(model_path), opts)
        self.model_path   = model_path
        self.model_version = model_path.stem  # e.g. "agrimitra_int8"
        logger.info(f"Loaded ONNX model: {model_path}")

    def _build_satellite_tensor(self, crop_type: str,
                                 planting_date: Optional[str] = None) -> np.ndarray:
        """
        Build a synthetic 12-week satellite tensor from crop type.
        Shape: (1, C, T, H, W) = (1, 4, 12, 224, 224)
        """
        ndvi_curve = NDVI_CURVES.get(crop_type, NDVI_CURVES["Rice"])
        rng = np.random.default_rng(42)

        frames = []
        for week, ndvi in enumerate(ndvi_curve):
            red  = np.full((IMG_H, IMG_W), 0.25 - 0.18 * ndvi, dtype=np.float32)
            grn  = np.full((IMG_H, IMG_W), 0.08 + 0.10 * ndvi, dtype=np.float32)
            nir  = np.full((IMG_H, IMG_W), 0.08 + 0.62 * ndvi, dtype=np.float32)
            swir = np.full((IMG_H, IMG_W), 0.22 - 0.08 * ndvi, dtype=np.float32)

            noise_scale = 0.02
            red  += rng.normal(0, noise_scale, (IMG_H, IMG_W)).astype(np.float32)
            nir  += rng.normal(0, noise_scale, (IMG_H, IMG_W)).astype(np.float32)

            frames.append(np.stack([red, grn, nir, swir]))  # (4, H, W)

        sat = np.stack(frames, axis=1)  # (4, 12, H, W)
        return sat[np.newaxis].astype(np.float32)  # (1, 4, 12, H, W)

    def _build_graph(self, iot_data: Optional[dict]) -> tuple[np.ndarray, np.ndarray, np.ndarray]:
        """
        Build a GRAPH_N_NODES-node graph from IoT sensor data.
        Returns (graph_nodes, edge_index, edge_attr).
        """
        rng = np.random.default_rng(123)

        # Node 0 = target farm; nodes 1+ = synthetic neighbours
        if iot_data:
            n0 = np.array([
                iot_data.get("soilN", 100.0),
                iot_data.get("soilP", 50.0),
                iot_data.get("soilK", 200.0),
                iot_data.get("moisture", 35.0),
                iot_data.get("ph", 6.5),
                iot_data.get("temperature", 28.0),
                iot_data.get("ndvi", 0.6),
                iot_data.get("elevation", 100.0),
            ], dtype=np.float32)
        else:
            n0 = np.array([100, 50, 200, 35, 6.5, 28, 0.6, 100], dtype=np.float32)

        nodes = [n0]
        for _ in range(GRAPH_N_NODES - 1):
            neighbour = n0 * rng.uniform(0.8, 1.2, (NODE_FEAT_DIM,)).astype(np.float32)
            nodes.append(neighbour)

        graph_nodes = np.stack(nodes)  # (N, 8)

        # Fully connected edges
        src, dst = [], []
        for i in range(GRAPH_N_NODES):
            for j in range(GRAPH_N_NODES):
                if i != j:
                    src.append(i); dst.append(j)
        edge_index = np.array([src, dst], dtype=np.int64)   # (2, E)
        edge_attr  = np.ones((edge_index.shape[1], 1), dtype=np.float32)

        return graph_nodes, edge_index, edge_attr

    def predict(self, crop_type: str,
                iot_data: Optional[dict] = None,
                planting_date: Optional[str] = None) -> dict:
        """
        Run ONNX inference and return prediction dict.

        Returns:
            {predictedYield, uncertaintyBand, modelVersion,
             inferenceLatencyMs, fertilizerAdvisory,
             irrigationAdvisory, marketAdvisory}
        """
        sat         = self._build_satellite_tensor(crop_type, planting_date)
        graph_nodes, edge_index, edge_attr = self._build_graph(iot_data)

        t0 = time.perf_counter()
        outputs = self.session.run(
            None,
            {
                "satellite":   sat,
                "graph_nodes": graph_nodes,
                "edge_index":  edge_index,
                "edge_attr":   edge_attr,
            }
        )
        latency_ms = int((time.perf_counter() - t0) * 1000)

        yield_val = float(outputs[0][0])
        yield_val = max(0.1, yield_val)

        return {
            "predictedYield":     round(yield_val, 2),
            "uncertaintyBand":    CROP_MAE.get(crop_type, 2.0),
            "modelVersion":       "agrimitra_int8_v1.0",
            "inferenceLatencyMs": latency_ms,
            **generate_advisories(crop_type, iot_data, planting_date, yield_val),
        }


# ── Advisory generation ───────────────────────────────────────────────────────

def generate_advisories(crop_type: str,
                         iot_data: Optional[dict],
                         planting_date: Optional[str],
                         yield_pred: float) -> dict:
    """Rule-based advisory generation matching the report's advisory cards."""

    # ── Fertilizer ────────────────────────────────────────────────────────────
    n = iot_data.get("soilN", 100) if iot_data else 100
    p = iot_data.get("soilP", 50)  if iot_data else 50
    k = iot_data.get("soilK", 200) if iot_data else 200

    fert_recs = {
        "Rice":      {"low_n": "Apply 60 kg/ha Urea in 2 splits (basal + tillering)",
                      "low_p": "Apply 40 kg/ha SSP at basal dose",
                      "ok":    "Apply 50 kg/ha Urea at top-dressing stage"},
        "Wheat":     {"low_n": "Apply 60 kg/ha Urea split in 3 doses",
                      "low_p": "Apply 35 kg/ha DAP at sowing",
                      "ok":    "Apply 40 kg/ha Urea at CRI stage"},
        "Soybean":   {"low_n": "Apply rhizobium seed treatment + 20 kg/ha starter N",
                      "low_p": "Apply 60 kg/ha SSP at sowing",
                      "ok":    "Soil N adequate; monitor for early yellowing"},
        "Sugarcane": {"low_n": "Apply 150 kg/ha Urea split over 4 months",
                      "low_p": "Apply 80 kg/ha SSP at planting",
                      "ok":    "Apply 120 kg/ha Urea in 3 splits (0, 60, 120 days)"},
    }

    crop_rec = fert_recs.get(crop_type, fert_recs["Rice"])
    if n < 80:
        fert_advisory = crop_rec["low_n"]
    elif p < 30:
        fert_advisory = crop_rec["low_p"]
    else:
        fert_advisory = crop_rec["ok"]

    # ── Irrigation ────────────────────────────────────────────────────────────
    moisture = iot_data.get("moisture", 35) if iot_data else 35
    if moisture < 20:
        irr_advisory = f"Soil moisture critically low ({moisture:.0f}%). Irrigate 30–40 mm immediately."
    elif moisture < 30:
        irr_advisory = f"Soil moisture low ({moisture:.0f}%). Schedule irrigation within 48 hours."
    else:
        irr_advisory = f"Soil moisture adequate ({moisture:.0f}%). Next irrigation in 7–10 days."

    # ── Market timing ─────────────────────────────────────────────────────────
    from datetime import datetime, timedelta
    msp_price = MSP.get(crop_type, 2000)
    maturity  = MATURITY_DAYS.get(crop_type, 120)

    if planting_date:
        try:
            plant_dt = datetime.fromisoformat(planting_date[:10])
        except (ValueError, AttributeError):
            plant_dt = datetime.today()
    else:
        plant_dt = datetime.today()

    harvest_dt   = plant_dt + timedelta(days=maturity)
    sell_start   = harvest_dt + timedelta(days=15)
    sell_end     = sell_start + timedelta(days=25)

    market_advisory = (
        f"Best selling window: {sell_start.strftime('%b %d')} – {sell_end.strftime('%b %d, %Y')}. "
        f"MSP: ₹{msp_price}/quintal. "
        f"Predicted yield {yield_pred:.1f} q/ha → est. ₹{int(yield_pred * msp_price):,}. "
        "Contact nearest APMC mandi 1 week before harvest."
    )

    return {
        "fertilizerAdvisory": fert_advisory,
        "irrigationAdvisory": irr_advisory,
        "marketAdvisory":     market_advisory,
    }

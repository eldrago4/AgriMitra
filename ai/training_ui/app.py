"""
AgriMitra Training UI — FastAPI web server.
Run: uvicorn ai.training_ui.app:app --port 8000 --reload
"""
import asyncio
import json
import logging
import threading
from pathlib import Path
from typing import Optional

from fastapi import FastAPI
from fastapi.responses import HTMLResponse, StreamingResponse
from fastapi.staticfiles import StaticFiles
from pydantic import BaseModel

import sys
sys.path.insert(0, str(Path(__file__).parent.parent))
from config import DATA_DIR, CKPT_DIR, ONNX_FP32, ONNX_INT8, EPOCHS

logger = logging.getLogger(__name__)
app = FastAPI(title="AgriMitra Training UI")

# ── Global training state ────────────────────────────────────────────────────

_training_thread: Optional[threading.Thread] = None

try:
    from training.train import GLOBAL_STATE, train, find_latest_checkpoint
    from data.graph_builder import build_node_stats
    import pandas as pd
    TRAIN_AVAILABLE = True
except ImportError as e:
    logger.warning(f"Training modules not available: {e}")
    TRAIN_AVAILABLE = False


def _iot_csv_path():
    for name in ["real_iot.csv", "synthetic_iot.csv"]:
        p = DATA_DIR / name
        if p.exists():
            return p
    return None


def _norm_stats_path():
    p = DATA_DIR / "norm_stats.npy"
    return p if p.exists() else None


def _start_training_thread(n_epochs: int = EPOCHS, resume: bool = False):
    global _training_thread

    if not TRAIN_AVAILABLE:
        return {"error": "Training dependencies not installed"}

    iot_csv = _iot_csv_path()
    if iot_csv is None:
        return {"error": "No IoT CSV found. Run data fetch first."}

    if _training_thread and _training_thread.is_alive():
        return {"error": "Training already running"}

    # Reset stop event
    GLOBAL_STATE.stop_event.clear()
    GLOBAL_STATE.pause_event.set()

    import pandas as pd
    df = pd.read_csv(iot_csv)
    node_stats = build_node_stats(df)
    norm_stats = _norm_stats_path()

    def run():
        try:
            train(
                iot_csv        = iot_csv,
                norm_stats_path= norm_stats,
                node_stats     = node_stats,
                n_epochs       = n_epochs,
                resume         = resume,
                state          = GLOBAL_STATE,
            )
        except Exception as exc:
            logger.exception(f"Training error: {exc}")
            GLOBAL_STATE.update(status="error")

    _training_thread = threading.Thread(target=run, daemon=True, name="training")
    _training_thread.start()
    return {"status": "started"}


# ── API endpoints ────────────────────────────────────────────────────────────

class StartRequest(BaseModel):
    n_epochs: int = EPOCHS
    resume:   bool = False


@app.post("/start")
async def start_training(req: StartRequest):
    if not TRAIN_AVAILABLE:
        return {"error": "torch/torch_geometric not installed"}
    result = _start_training_thread(req.n_epochs, req.resume)
    return result


@app.post("/pause")
async def pause_training():
    if not TRAIN_AVAILABLE:
        return {"status": "unavailable"}
    GLOBAL_STATE.pause_event.clear()
    GLOBAL_STATE.update(status="paused")
    return {"status": "paused"}


@app.post("/resume")
async def resume_training():
    if not TRAIN_AVAILABLE:
        return {"status": "unavailable"}
    GLOBAL_STATE.pause_event.set()
    GLOBAL_STATE.update(status="running")
    return {"status": "resumed"}


@app.post("/stop")
async def stop_training():
    if not TRAIN_AVAILABLE:
        return {"status": "unavailable"}
    GLOBAL_STATE.stop_event.set()
    GLOBAL_STATE.pause_event.set()  # unblock if paused
    GLOBAL_STATE.update(status="stopped")
    return {"status": "stopped"}


@app.get("/status")
async def get_status():
    if not TRAIN_AVAILABLE:
        return {"status": "idle", "error": "Training deps not installed"}
    snap = GLOBAL_STATE.snapshot()
    # Add ETA
    history = snap.get("history", [])
    if len(history) > 1:
        times = [h.get("epoch_time", 0) for h in history if "epoch_time" in h]
        avg_t = sum(times) / len(times) if times else 0
        remaining = (snap["epoch"] - len(history) + 1) * avg_t if avg_t > 0 else 0
        snap["eta_seconds"] = int(remaining)
    return snap


@app.get("/stream")
async def stream_metrics():
    """Server-Sent Events stream of training metrics."""
    async def event_generator():
        last_epoch = -1
        while True:
            if TRAIN_AVAILABLE:
                snap = GLOBAL_STATE.snapshot()
                if snap["epoch"] != last_epoch or snap["status"] in ("done", "stopped", "error"):
                    last_epoch = snap["epoch"]
                    yield f"data: {json.dumps(snap)}\n\n"
                    if snap["status"] in ("done", "stopped", "error"):
                        break
            await asyncio.sleep(2)

    return StreamingResponse(event_generator(), media_type="text/event-stream")


@app.post("/export")
async def export_model():
    """Export current best checkpoint to ONNX INT8."""
    if not TRAIN_AVAILABLE:
        return {"error": "Training deps not installed"}

    best = CKPT_DIR / "best_model.pt"
    if not best.exists():
        latest = find_latest_checkpoint() if TRAIN_AVAILABLE else None
        if not latest:
            return {"error": "No checkpoint found. Train first."}
        best = latest

    try:
        import torch
        from models.fusion import AgriMitraFusion
        from export.export_onnx import export_fp32
        from export.quantize_int8 import quantize_and_export

        model = AgriMitraFusion()
        state = torch.load(best, map_location="cpu")
        if isinstance(state, dict) and "model_state" in state:
            model.load_state_dict(state["model_state"])
        else:
            model.load_state_dict(state)

        export_fp32(model)
        quantize_and_export(model)

        return {
            "fp32_mb": round(ONNX_FP32.stat().st_size / 1e6, 1),
            "int8_mb": round(ONNX_INT8.stat().st_size / 1e6, 1),
            "fp32_path": str(ONNX_FP32),
            "int8_path": str(ONNX_INT8),
        }
    except Exception as e:
        return {"error": str(e)}


@app.get("/", response_class=HTMLResponse)
async def dashboard():
    template_path = Path(__file__).parent / "templates" / "training.html"
    if template_path.exists():
        return template_path.read_text(encoding="utf-8")
    return "<h1>AgriMitra Training UI</h1><p>Template not found.</p>"

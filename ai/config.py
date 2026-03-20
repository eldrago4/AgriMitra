"""Central configuration for the AgriMitra AI pipeline."""
import os
from pathlib import Path

# ── Directories ───────────────────────────────────────────────────────────────
ROOT_DIR   = Path(__file__).parent
DATA_DIR   = ROOT_DIR / "data"
TILES_DIR  = DATA_DIR / "real_tiles"
SYNTH_DIR  = DATA_DIR / "synthetic_tiles"
CKPT_DIR   = ROOT_DIR / "checkpoints"
OUT_DIR    = ROOT_DIR / "outputs"

for d in [DATA_DIR, TILES_DIR, SYNTH_DIR, CKPT_DIR, OUT_DIR]:
    d.mkdir(parents=True, exist_ok=True)

# ── Bhoonidhi API — always accessed via 1ved.cloud proxy ──────────────────────
# Credentials live only as Vercel env vars (BHOONIDHI_USER / BHOONIDHI_PASS).
# Local clients must never hold credentials; 1ved.cloud's IP is whitelisted by NRSC.
AGRIMITRA_PROXY = os.getenv("AGRIMITRA_PROXY", "https://1ved.cloud")
TOKEN_CACHE     = DATA_DIR / ".bhoonidhi_token.json"

# Default fetch area: Kankavli, Sindhudurg, Maharashtra
DEFAULT_BBOX   = [73.6, 16.4, 73.9, 16.7]   # [lon_min, lat_min, lon_max, lat_max]
DEFAULT_DATE   = "2022-06-01/2024-10-31"

# ── Satellite / Dataset ───────────────────────────────────────────────────────
N_WEEKS        = 12       # temporal stack depth
IMG_H          = 224
IMG_W          = 224
N_CHANNELS     = 4        # Red, Green, NIR, SWIR
NODE_FEAT_DIM  = 8        # soil N/P/K, moisture, pH, temp, NDVI, elevation
K_NEIGHBORS    = 10       # GNN graph k-NN radius

CROPS          = ["Rice", "Wheat", "Soybean", "Sugarcane"]
CROP_MAE       = {"Rice": 1.83, "Wheat": 2.05, "Soybean": 1.47, "Sugarcane": 3.21}

# ── Model ─────────────────────────────────────────────────────────────────────
SWIN_OUT_DIM   = 256
GNN_OUT_DIM    = 128
FUSION_DIMS    = [384, 256, 64, 1]
DROPOUT        = 0.3

# ── Training ──────────────────────────────────────────────────────────────────
SEED           = 42
BATCH_SIZE     = 4
EPOCHS         = 80
LR             = 1e-4
WEIGHT_DECAY   = 1e-2
T_MAX          = 50       # CosineAnnealingLR
HUBER_DELTA    = 1.5
GRAD_CLIP      = 1.0
VAL_SPLIT      = 0.15
TEST_SPLIT     = 0.15

# ── ONNX ──────────────────────────────────────────────────────────────────────
ONNX_OPSET     = 17
ONNX_FP32      = OUT_DIR / "agrimitra_fp32.onnx"
ONNX_INT8      = OUT_DIR / "agrimitra_int8.onnx"
MODEL_VERSION  = "agrimitra_int8_v1.0"
GRAPH_N_NODES  = 3        # fixed graph size for ONNX tracing (1 target + 2 neighbors)

# ── MSP 2023-24 (₹/quintal) ──────────────────────────────────────────────────
MSP = {
    "Rice":      2183,
    "Wheat":     2275,
    "Soybean":   4600,
    "Sugarcane":  315,
}

# Crop maturity days (from planting to harvest)
MATURITY_DAYS = {
    "Rice":     120,
    "Wheat":    135,
    "Soybean":   95,
    "Sugarcane": 365,
}

"""
INT8 dynamic quantization of the ONNX FP32 model.
Targets all Linear and LayerNorm layers using PyTorch's quantize_dynamic.
"""
import logging
from pathlib import Path

import torch
from torch.quantization import quantize_dynamic

import sys
sys.path.insert(0, str(Path(__file__).parent.parent))
from config import ONNX_FP32, ONNX_INT8, GRAPH_N_NODES, N_CHANNELS, N_WEEKS, IMG_H, IMG_W, NODE_FEAT_DIM
from models.fusion import AgriMitraFusion, OnnxExportWrapper

logger = logging.getLogger(__name__)
logging.basicConfig(level=logging.INFO, format="%(asctime)s %(levelname)s %(message)s")


def quantize_and_export(model: AgriMitraFusion,
                        output_path: Path = ONNX_INT8) -> Path:
    """
    Apply INT8 dynamic quantization to Linear and LayerNorm layers,
    then export to ONNX.
    """
    model.eval().cpu()

    logger.info("Applying INT8 dynamic quantization …")
    q_model = quantize_dynamic(
        model,
        {torch.nn.Linear, torch.nn.LayerNorm},
        dtype=torch.qint8
    )
    q_model.eval()

    wrapper = OnnxExportWrapper(q_model)
    wrapper.eval()

    # Dummy inputs (static shape — same as FP32 export)
    sat = torch.randn(1, N_CHANNELS, N_WEEKS, IMG_H, IMG_W)

    src, dst = [], []
    for i in range(GRAPH_N_NODES):
        for j in range(GRAPH_N_NODES):
            if i != j:
                src.append(i); dst.append(j)
    edge_index  = torch.tensor([src, dst], dtype=torch.long)
    n_edges     = edge_index.shape[1]
    graph_nodes = torch.randn(GRAPH_N_NODES, NODE_FEAT_DIM)
    edge_attr   = torch.ones(n_edges, 1)

    output_path.parent.mkdir(parents=True, exist_ok=True)

    logger.info(f"Exporting INT8 ONNX to {output_path} …")
    torch.onnx.export(
        wrapper,
        (sat, graph_nodes, edge_index, edge_attr),
        str(output_path),
        opset_version  = 17,
        input_names    = ["satellite", "graph_nodes", "edge_index", "edge_attr"],
        output_names   = ["yield_pred"],
        do_constant_folding = True,
    )

    fp32_mb = ONNX_FP32.stat().st_size / 1e6 if ONNX_FP32.exists() else 0
    int8_mb = output_path.stat().st_size / 1e6
    logger.info(f"FP32: {fp32_mb:.1f} MB  →  INT8: {int8_mb:.1f} MB"
                f"  (ratio: {fp32_mb/int8_mb:.2f}×)")

    return output_path


if __name__ == "__main__":
    import sys
    ckpt_path = sys.argv[1] if len(sys.argv) > 1 else None

    model = AgriMitraFusion()
    if ckpt_path and Path(ckpt_path).exists():
        state = torch.load(ckpt_path, map_location="cpu")
        if isinstance(state, dict) and "model_state" in state:
            model.load_state_dict(state["model_state"])
        else:
            model.load_state_dict(state)

    quantize_and_export(model)

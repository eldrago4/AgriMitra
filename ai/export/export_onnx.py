"""
Export trained AgriMitraFusion to ONNX (opset 17, FP32).
Uses OnnxExportWrapper to flatten PyG graph inputs into 3 tensors.
"""
import logging
from pathlib import Path

import torch

import sys
sys.path.insert(0, str(Path(__file__).parent.parent))
from config import ONNX_FP32, ONNX_OPSET, GRAPH_N_NODES, N_CHANNELS, N_WEEKS, IMG_H, IMG_W, NODE_FEAT_DIM
from models.fusion import AgriMitraFusion, OnnxExportWrapper

logger = logging.getLogger(__name__)
logging.basicConfig(level=logging.INFO, format="%(asctime)s %(levelname)s %(message)s")


def _make_dummy_inputs(n_nodes: int = GRAPH_N_NODES):
    """Create dummy tensors for ONNX tracing."""
    sat        = torch.randn(1, N_CHANNELS, N_WEEKS, IMG_H, IMG_W)

    # Fully-connected bidirectional graph (n_nodes nodes)
    src, dst = [], []
    for i in range(n_nodes):
        for j in range(n_nodes):
            if i != j:
                src.append(i)
                dst.append(j)
    edge_index = torch.tensor([src, dst], dtype=torch.long)
    n_edges    = edge_index.shape[1]

    graph_nodes = torch.randn(n_nodes, NODE_FEAT_DIM)
    edge_attr   = torch.ones(n_edges, 1)

    return sat, graph_nodes, edge_index, edge_attr


def export_fp32(model: AgriMitraFusion,
                output_path: Path = ONNX_FP32) -> Path:
    """Export model to ONNX FP32."""
    model.eval().cpu()
    wrapper = OnnxExportWrapper(model)
    wrapper.eval()

    sat, graph_nodes, edge_index, edge_attr = _make_dummy_inputs()

    output_path.parent.mkdir(parents=True, exist_ok=True)

    logger.info(f"Exporting FP32 ONNX to {output_path} …")
    torch.onnx.export(
        wrapper,
        (sat, graph_nodes, edge_index, edge_attr),
        str(output_path),
        opset_version  = ONNX_OPSET,
        input_names    = ["satellite", "graph_nodes", "edge_index", "edge_attr"],
        output_names   = ["yield_pred"],
        dynamic_axes   = {"satellite": {0: "batch_size"}},
        do_constant_folding = True,
    )

    size_mb = output_path.stat().st_size / 1e6
    logger.info(f"FP32 ONNX exported: {size_mb:.1f} MB")
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
        logger.info(f"Loaded weights from {ckpt_path}")
    else:
        logger.warning("No checkpoint provided — exporting untrained model")

    export_fp32(model)

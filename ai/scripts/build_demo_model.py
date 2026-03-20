#!/usr/bin/env python
"""
End-to-end: data → train (N epochs) → ONNX FP32 → ONNX INT8.
Then copies the INT8 model to deploy/ and mobile/Resources/Raw/.
"""
import argparse
import shutil
from pathlib import Path
import sys
sys.path.insert(0, str(Path(__file__).parent.parent))

from dotenv import load_dotenv
load_dotenv(Path(__file__).parent.parent / ".env")

import pandas as pd

ROOT = Path(__file__).parent.parent.parent   # project root

if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--demo-epochs", type=int, default=1,
                        help="Number of training epochs (1 for quick demo)")
    parser.add_argument("--resume", action="store_true",
                        help="Resume from latest checkpoint")
    args = parser.parse_args()

    from config import DATA_DIR, ONNX_FP32, ONNX_INT8, CKPT_DIR

    # 1. Load IoT CSV
    iot_csv = None
    for name in ["real_iot.csv", "synthetic_iot.csv"]:
        p = DATA_DIR / name
        if p.exists():
            iot_csv = p
            break

    if iot_csv is None:
        print("No data found — generating synthetic dataset …")
        from data.synthetic_generator import generate_synthetic_dataset
        _, iot_csv = generate_synthetic_dataset(n_records=200)

    # 2. Build node stats
    from data.graph_builder import build_node_stats
    norm_path = DATA_DIR / "norm_stats.npy"
    df = pd.read_csv(iot_csv)
    node_stats = build_node_stats(df)

    # 3. Train
    print(f"\n{'='*50}")
    print(f" AgriMitra Training  ({args.demo_epochs} epoch(s))")
    print(f"{'='*50}")

    from training.train import train, GLOBAL_STATE
    model = train(
        iot_csv         = iot_csv,
        norm_stats_path = norm_path,
        node_stats      = node_stats,
        n_epochs        = args.demo_epochs,
        resume          = args.resume,
        state           = GLOBAL_STATE,
    )

    # 4. ONNX export
    print("\nExporting to ONNX FP32 …")
    from export.export_onnx import export_fp32
    export_fp32(model)

    # 5. INT8 quantize
    print("Quantizing to INT8 …")
    from export.quantize_int8 import quantize_and_export
    quantize_and_export(model)

    # 6. Copy model to deploy/ and mobile/
    targets = [
        ROOT / "deploy" / "agrimitra_int8.onnx",
        ROOT / "mobile" / "AgriMitraMobile" / "Resources" / "Raw" / "agrimitra_int8.onnx",
    ]
    for dest in targets:
        dest.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(ONNX_INT8, dest)
        print(f"  Copied → {dest}")

    fp32_mb = ONNX_FP32.stat().st_size / 1e6
    int8_mb = ONNX_INT8.stat().st_size / 1e6
    print(f"\n{'='*50}")
    print(f" Done!")
    print(f"  FP32: {fp32_mb:.1f} MB  →  INT8: {int8_mb:.1f} MB  ({fp32_mb/int8_mb:.2f}x)")
    print(f"  Best checkpoint: {CKPT_DIR / 'best_model.pt'}")
    print(f"{'='*50}")

"""Evaluation utilities: MAE, RMSE, R², MAPE per crop."""
import numpy as np
import torch
from pathlib import Path
import sys
sys.path.insert(0, str(Path(__file__).parent.parent))


def evaluate(model, loader, device) -> dict:
    """
    Run model on loader, return regression metrics.
    Returns: {mae, rmse, r2, mape}
    """
    model.eval()
    preds_all, targets_all = [], []

    with torch.no_grad():
        for sat, graph, y in loader:
            sat   = sat.to(device)
            graph = graph.to(device)
            y     = y.to(device)
            pred  = model(sat, graph)
            preds_all.append(pred.cpu().numpy())
            targets_all.append(y.cpu().numpy())

    preds   = np.concatenate(preds_all)
    targets = np.concatenate(targets_all)

    return compute_metrics(preds, targets)


def compute_metrics(preds: np.ndarray, targets: np.ndarray) -> dict:
    mae  = float(np.mean(np.abs(preds - targets)))
    rmse = float(np.sqrt(np.mean((preds - targets) ** 2)))
    ss_res = np.sum((targets - preds) ** 2)
    ss_tot = np.sum((targets - targets.mean()) ** 2)
    r2   = float(1 - ss_res / (ss_tot + 1e-8))
    mape = float(np.mean(np.abs((targets - preds) / (np.abs(targets) + 1e-8))) * 100)
    return {"mae": mae, "rmse": rmse, "r2": r2, "mape": mape}

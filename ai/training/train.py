"""
AgriMitra training loop with AdamW + CosineAnnealingLR + HuberLoss.
Supports checkpointing for pause/resume via the Training UI.
"""
import logging
import threading
import time
from pathlib import Path
from typing import Optional, Callable

import numpy as np
import torch
import torch.nn as nn
from torch.utils.data import DataLoader
from torch_geometric.loader import DataLoader as PyGDataLoader

import sys
sys.path.insert(0, str(Path(__file__).parent.parent))
from config import (
    BATCH_SIZE, EPOCHS, LR, WEIGHT_DECAY, T_MAX, HUBER_DELTA,
    GRAD_CLIP, SEED, CKPT_DIR, DATA_DIR
)
from models.fusion import AgriMitraFusion
from training.evaluate import evaluate

logger = logging.getLogger(__name__)
logging.basicConfig(level=logging.INFO, format="%(asctime)s %(levelname)s %(message)s")

# ── Thread-safe training state (used by Training UI) ─────────────────────────

class TrainingState:
    def __init__(self):
        self.lock       = threading.Lock()
        self.epoch      = 0
        self.batch      = 0
        self.train_loss = 0.0
        self.val_mae    = 0.0
        self.val_r2     = 0.0
        self.val_mape   = 0.0
        self.status     = "idle"      # idle | running | paused | stopped | done
        self.history    = []          # list of epoch metric dicts
        self.pause_event = threading.Event()
        self.stop_event  = threading.Event()
        self.pause_event.set()        # not paused initially

    def update(self, **kwargs):
        with self.lock:
            for k, v in kwargs.items():
                setattr(self, k, v)

    def snapshot(self) -> dict:
        with self.lock:
            return {
                "epoch":      self.epoch,
                "batch":      self.batch,
                "train_loss": round(self.train_loss, 4),
                "val_mae":    round(self.val_mae, 4),
                "val_r2":     round(self.val_r2, 4),
                "val_mape":   round(self.val_mape, 4),
                "status":     self.status,
                "history":    list(self.history),
            }


GLOBAL_STATE = TrainingState()


# ── Custom collate for mixed PyG + tensor batches ────────────────────────────

def _collate(batch):
    """Collate (sat_tensor, graph_data, y) tuples."""
    from torch_geometric.data import Batch
    sats    = torch.stack([b[0] for b in batch])
    graphs  = Batch.from_data_list([b[1] for b in batch])
    ys      = torch.stack([b[2] for b in batch])
    return sats, graphs, ys


def get_dataloaders(iot_csv: Path, norm_stats_path: Path,
                    node_stats: dict) -> tuple:
    """Create train/val/test DataLoaders from the IoT CSV."""
    from data.dataset import AgriDataset, make_splits
    from torch_geometric.data import Data

    train_ids, val_ids, test_ids = make_splits(iot_csv, seed=SEED)

    train_ds = AgriDataset(iot_csv, norm_stats_path, node_stats, split_ids=train_ids)
    val_ds   = AgriDataset(iot_csv, norm_stats_path, node_stats, split_ids=val_ids)
    test_ds  = AgriDataset(iot_csv, norm_stats_path, node_stats, split_ids=test_ids)

    kwargs = dict(batch_size=BATCH_SIZE, num_workers=0,
                  pin_memory=torch.cuda.is_available(), collate_fn=_collate)

    train_loader = DataLoader(train_ds, shuffle=True,  **kwargs)
    val_loader   = DataLoader(val_ds,   shuffle=False, **kwargs)
    test_loader  = DataLoader(test_ds,  shuffle=False, **kwargs)

    logger.info(f"Dataset: {len(train_ds)} train / {len(val_ds)} val / {len(test_ds)} test")
    return train_loader, val_loader, test_loader


def find_latest_checkpoint() -> Optional[Path]:
    """Return the most recent checkpoint file, or None."""
    ckpts = sorted(CKPT_DIR.glob("epoch_*.pt"))
    return ckpts[-1] if ckpts else None


def train(iot_csv: Path,
          norm_stats_path: Path,
          node_stats: dict,
          n_epochs: int = EPOCHS,
          resume: bool = False,
          state: Optional[TrainingState] = None,
          on_epoch_end: Optional[Callable] = None) -> AgriMitraFusion:
    """
    Main training function. Supports pause/resume via TrainingState.

    Args:
        iot_csv:           path to IoT CSV
        norm_stats_path:   path to normalisation stats .npy
        node_stats:        node feature normalisation dict
        n_epochs:          total epochs to train
        resume:            resume from latest checkpoint
        state:             TrainingState for UI control (uses GLOBAL_STATE if None)
        on_epoch_end:      optional callback(epoch, metrics_dict) for SSE streaming

    Returns:
        Trained AgriMitraFusion model (on CPU)
    """
    if state is None:
        state = GLOBAL_STATE

    torch.manual_seed(SEED)
    device = torch.device("cuda" if torch.cuda.is_available() else "cpu")
    logger.info(f"Training on: {device}")

    model     = AgriMitraFusion().to(device)
    optimizer = torch.optim.AdamW(model.parameters(), lr=LR, weight_decay=WEIGHT_DECAY)
    scheduler = torch.optim.lr_scheduler.CosineAnnealingLR(optimizer, T_max=T_MAX)
    criterion = nn.HuberLoss(delta=HUBER_DELTA)

    start_epoch  = 0
    best_val_mae = float("inf")

    # Resume from checkpoint
    if resume:
        ckpt_path = find_latest_checkpoint()
        if ckpt_path:
            logger.info(f"Resuming from {ckpt_path}")
            ckpt = torch.load(ckpt_path, map_location=device)
            model.load_state_dict(ckpt["model_state"])
            optimizer.load_state_dict(ckpt["optimizer_state"])
            scheduler.load_state_dict(ckpt["scheduler_state"])
            start_epoch  = ckpt["epoch"] + 1
            best_val_mae = ckpt.get("best_val_mae", best_val_mae)
            state.history = ckpt.get("history", [])
            logger.info(f"Resumed at epoch {start_epoch}")

    train_loader, val_loader, _ = get_dataloaders(iot_csv, norm_stats_path, node_stats)

    state.update(status="running", epoch=start_epoch)

    epoch_times = []

    for epoch in range(start_epoch, n_epochs):

        # ── Pause check ──────────────────────────────────────────────────────
        state.update(status="paused") if not state.pause_event.is_set() else None
        state.pause_event.wait()  # blocks until resumed
        if state.stop_event.is_set():
            break
        state.update(status="running", epoch=epoch)

        t0 = time.time()
        model.train()
        epoch_loss = 0.0
        n_batches  = len(train_loader)

        for batch_idx, (sat, graph, y) in enumerate(train_loader):
            # Pause mid-epoch
            if not state.pause_event.is_set():
                state.update(status="paused")
                state.pause_event.wait()
                if state.stop_event.is_set():
                    break
                state.update(status="running")

            sat  = sat.to(device)
            y    = y.to(device)
            graph = graph.to(device)

            pred = model(sat, graph)
            loss = criterion(pred, y)

            optimizer.zero_grad()
            loss.backward()
            torch.nn.utils.clip_grad_norm_(model.parameters(), GRAD_CLIP)
            optimizer.step()

            epoch_loss += loss.item()
            state.update(batch=batch_idx + 1, train_loss=loss.item())

        if state.stop_event.is_set():
            break

        scheduler.step()
        avg_loss = epoch_loss / max(n_batches, 1)

        # Validation
        val_metrics = evaluate(model, val_loader, device)
        val_mae  = val_metrics["mae"]
        val_r2   = val_metrics["r2"]
        val_mape = val_metrics["mape"]

        epoch_time = time.time() - t0
        epoch_times.append(epoch_time)

        metrics = {
            "epoch":      epoch,
            "train_loss": round(avg_loss, 4),
            "val_mae":    round(val_mae, 4),
            "val_r2":     round(val_r2, 4),
            "val_mape":   round(val_mape, 4),
            "lr":         round(scheduler.get_last_lr()[0], 6),
            "epoch_time": round(epoch_time, 1),
        }

        state.history.append(metrics)
        state.update(val_mae=val_mae, val_r2=val_r2, val_mape=val_mape,
                     train_loss=avg_loss)

        logger.info(
            f"Epoch {epoch+1:3d}/{n_epochs}  loss={avg_loss:.4f}  "
            f"val_MAE={val_mae:.3f}  val_R²={val_r2:.3f}  "
            f"MAPE={val_mape:.1f}%  ({epoch_time:.0f}s)"
        )

        # Save checkpoint every epoch
        ckpt_path = CKPT_DIR / f"epoch_{epoch:03d}.pt"
        torch.save({
            "epoch":           epoch,
            "model_state":     model.state_dict(),
            "optimizer_state": optimizer.state_dict(),
            "scheduler_state": scheduler.state_dict(),
            "best_val_mae":    best_val_mae,
            "history":         state.history,
        }, ckpt_path)

        # Keep only the last 3 checkpoints to save disk space
        old_ckpts = sorted(CKPT_DIR.glob("epoch_*.pt"))[:-3]
        for old in old_ckpts:
            old.unlink(missing_ok=True)

        if val_mae < best_val_mae:
            best_val_mae = val_mae
            best_path = CKPT_DIR / "best_model.pt"
            torch.save(model.state_dict(), best_path)
            logger.info(f"  ✓ New best model (val_MAE={best_val_mae:.3f})")

        if on_epoch_end:
            on_epoch_end(epoch, metrics)

    state.update(status="done")
    model.cpu()
    return model

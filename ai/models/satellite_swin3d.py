"""Swin3D-Tiny backbone adapted for 4-channel satellite time-series input."""
import torch
import torch.nn as nn
from pathlib import Path
import sys
sys.path.insert(0, str(Path(__file__).parent.parent))
from config import N_CHANNELS, SWIN_OUT_DIM


class SatelliteSwin3D(nn.Module):
    """
    Extracts a 256-dim spatiotemporal feature from a
    (B, C=4, T=12, H=224, W=224) satellite tensor.

    Uses Swin3D-Tiny pretrained on Kinetics-400.
    A learned 1×1 Conv3d adapter projects 4→3 channels before the backbone.
    The classification head is replaced with a regression projection head.
    """

    def __init__(self, out_features: int = SWIN_OUT_DIM):
        super().__init__()

        # 4→3 channel adapter (learned, not frozen)
        self.channel_adapter = nn.Conv3d(
            in_channels=N_CHANNELS, out_channels=3,
            kernel_size=1, bias=False
        )
        nn.init.xavier_uniform_(self.channel_adapter.weight)

        # Swin3D-Tiny backbone
        try:
            from torchvision.models.video import swin3d_t, Swin3D_T_Weights
            self.backbone = swin3d_t(weights=Swin3D_T_Weights.KINETICS400_V1)
        except Exception:
            # Weights unavailable (offline) — load without pretrained weights
            from torchvision.models.video import swin3d_t
            self.backbone = swin3d_t(weights=None)

        # Determine the in_features of the final linear layer
        # Swin3D_T head is Sequential(LayerNorm, Linear) — we want Linear's in_features
        try:
            in_feats = self.backbone.head[1].in_features
        except (AttributeError, IndexError, TypeError):
            in_feats = 768  # Swin3D-Tiny default

        # Replace classification head with projection head
        self.backbone.head = nn.Sequential(
            nn.AdaptiveAvgPool3d(1),
            nn.Flatten(),
            nn.Linear(in_feats, out_features),
            nn.LayerNorm(out_features),
        )

    def forward(self, x: torch.Tensor) -> torch.Tensor:
        """
        Args:
            x: (B, 4, T, H, W) — 4-channel satellite stack
        Returns:
            (B, 256) spatiotemporal feature vector
        """
        x = self.channel_adapter(x)   # (B, 3, T, H, W)
        return self.backbone(x)       # (B, 256)

"""GATv2-based Graph Neural Network for farm field spatial encoding."""
import torch
import torch.nn as nn
import torch.nn.functional as F
from pathlib import Path
import sys
sys.path.insert(0, str(Path(__file__).parent.parent))
from config import NODE_FEAT_DIM, GNN_OUT_DIM


class FarmGNN(nn.Module):
    """
    Encodes a farm-field graph into a 128-dim spatial feature vector.

    Node features (8):  [soil_N, soil_P, soil_K, moisture, pH, temp, NDVI, elevation]
    Edge features (1):  [inverse_distance]

    Architecture:
        GATv2Conv(8  → 64×4)  + LayerNorm + ELU
        GATv2Conv(256 → 128×1) + LayerNorm + ELU
        global_mean_pool  →  (B, 128)
    """

    def __init__(self, node_feat_dim: int = NODE_FEAT_DIM,
                 out_features: int = GNN_OUT_DIM):
        super().__init__()
        from torch_geometric.nn import GATv2Conv, global_mean_pool

        self.conv1 = GATv2Conv(node_feat_dim, 64, heads=4,
                               edge_dim=1, concat=True)   # → (N, 256)
        self.conv2 = GATv2Conv(64 * 4, out_features, heads=1,
                               edge_dim=1, concat=False)  # → (N, 128)
        self.norm1 = nn.LayerNorm(64 * 4)
        self.norm2 = nn.LayerNorm(out_features)
        self.pool  = global_mean_pool

    def forward(self, data) -> torch.Tensor:
        """
        Args:
            data: torch_geometric.data.Data with .x (N,8), .edge_index (2,E),
                  .edge_attr (E,1), .batch (N,)
        Returns:
            (B, 128) spatial feature vector
        """
        x          = data.x           # (N, 8)
        edge_index = data.edge_index  # (2, E)
        edge_attr  = data.edge_attr   # (E, 1)
        batch      = data.batch       # (N,) — None for single graph

        x = F.elu(self.norm1(self.conv1(x, edge_index, edge_attr)))
        x = F.elu(self.norm2(self.conv2(x, edge_index, edge_attr)))

        if batch is None:
            batch = torch.zeros(x.size(0), dtype=torch.long, device=x.device)

        return self.pool(x, batch)    # (B, 128)

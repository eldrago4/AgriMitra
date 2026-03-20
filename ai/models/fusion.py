"""AgriMitraFusion — multi-modal fusion model combining Swin3D + GNN."""
import torch
import torch.nn as nn
from pathlib import Path
import sys
sys.path.insert(0, str(Path(__file__).parent.parent))
from config import SWIN_OUT_DIM, GNN_OUT_DIM, DROPOUT
from models.satellite_swin3d import SatelliteSwin3D
from models.farm_gnn import FarmGNN


class AgriMitraFusion(nn.Module):
    """
    End-to-end fusion model:
        satellite_tensor (B,4,12,224,224) → Swin3D → (B,256)
        graph_data                         → GNN   → (B,128)
        concat → (B,384) → MLP → (B,1)  [yield in q/ha]
    """

    def __init__(self):
        super().__init__()
        self.swin = SatelliteSwin3D(out_features=SWIN_OUT_DIM)
        self.gnn  = FarmGNN(out_features=GNN_OUT_DIM)

        fused_dim = SWIN_OUT_DIM + GNN_OUT_DIM  # 384

        self.fusion = nn.Sequential(
            nn.Linear(fused_dim, 256),
            nn.GELU(),
            nn.Dropout(DROPOUT),
            nn.Linear(256, 64),
            nn.GELU(),
            nn.Linear(64, 1),
        )

    def forward(self, sat_tensor: torch.Tensor, graph_data) -> torch.Tensor:
        """
        Args:
            sat_tensor: (B, 4, 12, 224, 224)
            graph_data: torch_geometric.data.Data (or Batch)
        Returns:
            (B,) yield predictions in q/ha
        """
        v     = self.swin(sat_tensor)          # (B, 256)
        g     = self.gnn(graph_data)           # (B, 128)
        fused = torch.cat([v, g], dim=-1)      # (B, 384)
        out   = self.fusion(fused)             # (B, 1)
        return out.squeeze(-1)                 # (B,)


class OnnxExportWrapper(nn.Module):
    """
    Thin wrapper around AgriMitraFusion that accepts flat tensors
    (satellite, graph_nodes, edge_index) instead of a PyG Data object,
    making the model ONNX-traceable with static graph topology.

    Fixed graph size: GRAPH_N_NODES nodes, edges fully connected (bidirectional).
    """

    def __init__(self, fusion_model: AgriMitraFusion):
        super().__init__()
        self.fusion = fusion_model

    def forward(self,
                satellite:   torch.Tensor,   # (1, 4, 12, 224, 224)
                graph_nodes: torch.Tensor,   # (N, 8)
                edge_index:  torch.Tensor,   # (2, E)   int64
                edge_attr:   torch.Tensor,   # (E, 1)   float32
                ) -> torch.Tensor:           # (1,)
        """Reconstruct a minimal PyG Data object and run forward pass."""
        from torch_geometric.data import Data
        data = Data(
            x          = graph_nodes,
            edge_index = edge_index,
            edge_attr  = edge_attr,
        )
        return self.fusion(satellite, data)

#!/usr/bin/env python
"""CLI: generate synthetic demo dataset."""
import argparse
from pathlib import Path
import sys
sys.path.insert(0, str(Path(__file__).parent.parent))

from dotenv import load_dotenv
load_dotenv(Path(__file__).parent.parent / ".env")

from data.synthetic_generator import generate_synthetic_dataset

if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="Generate synthetic AgriMitra training data")
    parser.add_argument("--n-records", type=int, default=200)
    args = parser.parse_args()

    record_dirs, csv_path = generate_synthetic_dataset(n_records=args.n_records)
    print(f"\n✓ Generated {len(record_dirs)} records")
    print(f"✓ IoT CSV: {csv_path}")

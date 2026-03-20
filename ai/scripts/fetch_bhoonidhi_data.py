#!/usr/bin/env python
"""CLI: fetch real ISRO satellite data from Bhoonidhi API for Kankavli area."""
import argparse
from pathlib import Path
import sys
sys.path.insert(0, str(Path(__file__).parent.parent))

from dotenv import load_dotenv
load_dotenv(Path(__file__).parent.parent / ".env")

from data.bhoonidhi_fetcher import fetch_kankavli_data

if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="Fetch ISRO satellite data via Bhoonidhi API")
    parser.add_argument("--bbox",      type=str,  default="73.6,16.4,73.9,16.7",
                        help="lon_min,lat_min,lon_max,lat_max")
    parser.add_argument("--n-records", type=int,  default=50)
    parser.add_argument("--date",      type=str,  default="2022-06-01/2024-10-31")
    args = parser.parse_args()

    bbox = [float(x) for x in args.bbox.split(",")]

    record_dirs, csv_path = fetch_kankavli_data(
        bbox=bbox, n_records=args.n_records, date_range=args.date
    )
    print(f"\n✓ Fetched {len(record_dirs)} records")
    print(f"✓ IoT CSV: {csv_path}")

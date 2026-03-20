/**
 * GET /api/market/mandi?lat=16.5&lon=73.7
 * Returns the nearest APMC mandi from a hardcoded list.
 */
import { NextRequest, NextResponse } from "next/server";

interface Mandi {
  name:     string;
  district: string;
  state:    string;
  lat:      number;
  lon:      number;
  phone:    string;
  isOpen:   boolean;
}

const MANDIS: Mandi[] = [
  { name: "Kalamna Market Yard",  district: "Nagpur",      state: "MH", lat: 21.10, lon: 79.05, phone: "+91-712-2710001", isOpen: true },
  { name: "Kankavli APMC",        district: "Sindhudurg",  state: "MH", lat: 16.55, lon: 73.70, phone: "+91-2367-232456", isOpen: true },
  { name: "Kolhapur APMC",        district: "Kolhapur",    state: "MH", lat: 16.70, lon: 74.24, phone: "+91-231-2644001", isOpen: true },
  { name: "Sangli APMC",          district: "Sangli",      state: "MH", lat: 16.85, lon: 74.57, phone: "+91-233-2325001", isOpen: true },
  { name: "Pune APMC",            district: "Pune",        state: "MH", lat: 18.52, lon: 73.86, phone: "+91-20-24262222", isOpen: true },
  { name: "Nashik APMC",          district: "Nashik",      state: "MH", lat: 19.99, lon: 73.79, phone: "+91-253-2310001", isOpen: true },
  { name: "Aurangabad APMC",      district: "Aurangabad",  state: "MH", lat: 19.88, lon: 75.34, phone: "+91-240-2330001", isOpen: false },
  { name: "Latur APMC",           district: "Latur",       state: "MH", lat: 18.40, lon: 76.56, phone: "+91-2382-220001", isOpen: true },
];

function haversineKm(lat1: number, lon1: number, lat2: number, lon2: number): number {
  const R    = 6371;
  const phi1 = (lat1 * Math.PI) / 180;
  const phi2 = (lat2 * Math.PI) / 180;
  const dphi = ((lat2 - lat1) * Math.PI) / 180;
  const dlam = ((lon2 - lon1) * Math.PI) / 180;
  const a    = Math.sin(dphi / 2) ** 2 + Math.cos(phi1) * Math.cos(phi2) * Math.sin(dlam / 2) ** 2;
  return R * 2 * Math.asin(Math.sqrt(a));
}

export async function GET(req: NextRequest) {
  const latStr = req.nextUrl.searchParams.get("lat");
  const lonStr = req.nextUrl.searchParams.get("lon");

  const lat = parseFloat(latStr ?? "16.55");
  const lon = parseFloat(lonStr ?? "73.70");

  if (isNaN(lat) || isNaN(lon)) {
    return NextResponse.json({ error: "Invalid lat/lon" }, { status: 400 });
  }

  // Sort mandis by distance
  const withDist = MANDIS.map((m) => ({
    ...m,
    distanceKm: Math.round(haversineKm(lat, lon, m.lat, m.lon) * 10) / 10,
  })).sort((a, b) => a.distanceKm - b.distanceKm);

  return NextResponse.json({
    nearest: withDist[0],
    all:     withDist.slice(0, 5),
  });
}

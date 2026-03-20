/**
 * GET /api/market/msp
 * Returns the 2025-26 MSP (Minimum Support Price) table.
 * Field names match the mobile MspEntry model: crop, variety, price, unit.
 */
import { NextResponse } from "next/server";

const MSP_DATA: { crop: string; variety: string; price: number; unit: string; group: string }[] = [
  // Cereals
  { crop: "Paddy (Common)",         variety: "Common",        price: 2369,  unit: "/qtl", group: "Cereals" },
  { crop: "Paddy (Grade A)",        variety: "Grade A",       price: 2389,  unit: "/qtl", group: "Cereals" },
  { crop: "Wheat",                  variety: "All",           price: 2585,  unit: "/qtl", group: "Cereals" },
  { crop: "Jowar (Hybrid)",         variety: "Hybrid",        price: 3649,  unit: "/qtl", group: "Cereals" },
  { crop: "Jowar (Maldandi)",       variety: "Maldandi",      price: 3699,  unit: "/qtl", group: "Cereals" },
  { crop: "Bajra",                  variety: "All",           price: 2625,  unit: "/qtl", group: "Cereals" },
  { crop: "Maize",                  variety: "All",           price: 2225,  unit: "/qtl", group: "Cereals" },
  { crop: "Ragi",                   variety: "All",           price: 4290,  unit: "/qtl", group: "Cereals" },
  // Pulses
  { crop: "Arhar / Tur",            variety: "All",           price: 7550,  unit: "/qtl", group: "Pulses" },
  { crop: "Moong",                  variety: "All",           price: 8682,  unit: "/qtl", group: "Pulses" },
  { crop: "Urad",                   variety: "All",           price: 7400,  unit: "/qtl", group: "Pulses" },
  { crop: "Groundnut",              variety: "All",           price: 6783,  unit: "/qtl", group: "Oil Seeds" },
  { crop: "Sunflower Seed",         variety: "All",           price: 7280,  unit: "/qtl", group: "Oil Seeds" },
  { crop: "Soybean (Yellow)",       variety: "Yellow",        price: 4892,  unit: "/qtl", group: "Oil Seeds" },
  { crop: "Sesamum",                variety: "All",           price: 9267,  unit: "/qtl", group: "Oil Seeds" },
  { crop: "Nigerseed",              variety: "All",           price: 8717,  unit: "/qtl", group: "Oil Seeds" },
  // Fibre
  { crop: "Cotton (Medium)",        variety: "Medium Staple", price: 7121,  unit: "/qtl", group: "Fibre Crops" },
  { crop: "Cotton (Long)",          variety: "Long Staple",   price: 7521,  unit: "/qtl", group: "Fibre Crops" },
  // Rabi pulses & oilseeds
  { crop: "Gram",                   variety: "All",           price: 5650,  unit: "/qtl", group: "Pulses" },
  { crop: "Masur (Lentil)",         variety: "All",           price: 6700,  unit: "/qtl", group: "Pulses" },
  { crop: "Rapeseed / Mustard",     variety: "All",           price: 5950,  unit: "/qtl", group: "Oil Seeds" },
  { crop: "Safflower",              variety: "All",           price: 5800,  unit: "/qtl", group: "Oil Seeds" },
  // Sindhudurg key crops
  { crop: "Cashewnut",              variety: "Raw",           price: 7800,  unit: "/qtl", group: "Plantation" },
  { crop: "Coconut",                variety: "Milling Copra", price: 11000, unit: "/qtl", group: "Plantation" },
];

export async function GET() {
  return NextResponse.json({
    year: "2025-26",
    unit: "INR",
    data: MSP_DATA,
  });
}

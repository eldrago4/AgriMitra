/**
 * POST /api/farmers  — Register a new farmer
 * GET  /api/farmers  — List farmers (admin use)
 */
import { NextRequest, NextResponse } from "next/server";
import { PrismaClient } from "@prisma/client";

const prisma = new PrismaClient();

export async function POST(req: NextRequest) {
  try {
    const { name, phone, village, district, language } = await req.json();

    if (!name || !phone) {
      return NextResponse.json({ error: "name and phone are required" }, { status: 400 });
    }

    const farmer = await prisma.farmer.upsert({
      where:  { phone },
      update: { name, village, district, language },
      create: { name, phone, village: village ?? "", district: district ?? "", language: language ?? "en" },
    });

    return NextResponse.json(farmer, { status: 201 });
  } catch (err) {
    console.error("/api/farmers POST error:", err);
    return NextResponse.json({ error: "Internal server error" }, { status: 500 });
  }
}

export async function GET() {
  try {
    const farmers = await prisma.farmer.findMany({
      orderBy: { createdAt: "desc" },
      take: 100,
    });
    return NextResponse.json(farmers);
  } catch (err) {
    return NextResponse.json({ error: "Internal server error" }, { status: 500 });
  }
}

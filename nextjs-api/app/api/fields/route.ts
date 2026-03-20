/**
 * POST /api/fields   — Save a new farm polygon
 * GET  /api/fields   — Get fields for a farmer (?farmerId=xxx)
 */
import { NextRequest, NextResponse } from "next/server";
import { PrismaClient } from "@prisma/client";

const prisma = new PrismaClient();

export async function POST(req: NextRequest) {
  try {
    const { farmerId, polygonGeoJson, areaHectares, label } = await req.json();

    if (!farmerId || !polygonGeoJson) {
      return NextResponse.json(
        { error: "farmerId and polygonGeoJson are required" },
        { status: 400 }
      );
    }

    const field = await prisma.farmField.create({
      data: { farmerId, polygonGeoJson, areaHectares: areaHectares ?? 0, label: label ?? "My Field" },
    });

    return NextResponse.json(field, { status: 201 });
  } catch (err) {
    console.error("/api/fields POST error:", err);
    return NextResponse.json({ error: "Internal server error" }, { status: 500 });
  }
}

export async function GET(req: NextRequest) {
  const farmerId = req.nextUrl.searchParams.get("farmerId");
  if (!farmerId) {
    return NextResponse.json({ error: "farmerId query param required" }, { status: 400 });
  }

  try {
    const fields = await prisma.farmField.findMany({
      where:   { farmerId },
      include: { predictions: { orderBy: { createdAt: "desc" }, take: 1 } },
      orderBy: { createdAt: "desc" },
    });
    return NextResponse.json(fields);
  } catch (err) {
    return NextResponse.json({ error: "Internal server error" }, { status: 500 });
  }
}

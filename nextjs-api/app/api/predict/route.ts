/**
 * POST /api/predict
 * Forwards the prediction request to the 1ved.cloud inference service,
 * saves the result to the DB, and returns it to the mobile app.
 */
import { NextRequest, NextResponse } from "next/server";
import { PrismaClient } from "@prisma/client";

const prisma = new PrismaClient();

const INFER_URL = process.env.INFER_URL ?? "http://localhost:8001";

export async function POST(req: NextRequest) {
  try {
    const body = await req.json();
    const { farmCoordinates, cropType, iotSensorData, plantingDate, fieldId } = body;

    if (!cropType || !farmCoordinates) {
      return NextResponse.json(
        { error: "cropType and farmCoordinates are required" },
        { status: 400 }
      );
    }

    // Forward to inference service
    const inferRes = await fetch(`${INFER_URL}/infer`, {
      method:  "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ farmCoordinates, cropType, iotSensorData, plantingDate }),
    });

    if (!inferRes.ok) {
      const errText = await inferRes.text();
      return NextResponse.json(
        { error: `Inference service error: ${errText}` },
        { status: 502 }
      );
    }

    const result = await inferRes.json();

    // Persist to DB if fieldId provided
    if (fieldId) {
      try {
        await prisma.prediction.create({
          data: {
            fieldId,
            cropType,
            plantingDate,
            predictedYield:     result.predictedYield,
            uncertaintyBand:    result.uncertaintyBand,
            fertilizerAdvisory: result.fertilizerAdvisory,
            irrigationAdvisory: result.irrigationAdvisory,
            marketAdvisory:     result.marketAdvisory,
            inferenceLatencyMs: result.inferenceLatencyMs,
            modelVersion:       result.modelVersion,
          },
        });
      } catch (dbErr) {
        console.error("DB write failed:", dbErr);
        // Non-fatal — still return the prediction
      }
    }

    return NextResponse.json(result);
  } catch (err) {
    console.error("/api/predict error:", err);
    return NextResponse.json({ error: "Internal server error" }, { status: 500 });
  }
}

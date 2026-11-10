// GET /api/sql/schema
// The database's tables with their columns (name, type, key, indexed),
// indexes and exact row counts, plus the server version.
// 200 -> SchemaResponse   (errors: { error } with 503 config / 500)
import { connection } from "next/server";
import { errorResponse } from "@/lib/db";
import { loadSchema } from "@/lib/sqlServer";

export async function GET() {
  // Always read the live database at request time (never prerendered).
  await connection();
  try {
    return Response.json(await loadSchema());
  } catch (error) {
    return errorResponse(error);
  }
}

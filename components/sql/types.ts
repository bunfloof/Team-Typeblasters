// Shapes of the SQL console's API (/api/sql, /api/sql/schema, /api/sql/export).
// Plain types: safe to import from the routes and from the client.

// POST /api/sql -> one result set.
export type SqlRunResponse = {
  columns: { name: string; type: number | null }[]; // type = MySQL column type code (see columnTypeName)
  rows: Record<string, unknown>[];
  rowCount: number; // rows returned (truncated = the query has more than these)
  truncated: boolean; // rows was cut to maxRows
  ms: number;
};

// Every SQL route answers this (non-2xx status) when something goes wrong.
export type SqlErrorBody = {
  error: string; // MySQL's message, verbatim
  code?: string; // e.g. ER_PARSE_ERROR, PROTOCOL_SEQUENCE_TIMEOUT
  errno?: number;
  sqlState?: string;
  readOnly?: boolean; // refused by the read-only rule (400)
  config?: boolean; // .env.local is incomplete (503)
};

export type SchemaColumn = {
  name: string;
  type: string; // e.g. "varchar(64)", "json", "datetime(6)"
  nullable: boolean;
  key: string; // PRI, UNI, MUL or ""
  indexed: boolean; // first column of some index: filtering on it is fast
  comment: string;
};

export type SchemaIndex = { name: string; unique: boolean; columns: string[] };

export type SchemaTable = {
  name: string;
  kind: string; // BASE TABLE or VIEW
  rows: number | null; // exact COUNT(*) for tables (null if it failed)
  columns: SchemaColumn[];
  indexes: SchemaIndex[];
};

// GET /api/sql/schema
export type SchemaResponse = {
  database: string;
  version: string; // VERSION(): tells MySQL from MariaDB
  tables: SchemaTable[];
};

// MySQL column type codes (as mysql2 reports them) -> readable names.
const TYPE_NAMES: Record<number, string> = {
  0: "DECIMAL",
  1: "TINYINT",
  2: "SMALLINT",
  3: "INT",
  4: "FLOAT",
  5: "DOUBLE",
  6: "NULL",
  7: "TIMESTAMP",
  8: "BIGINT",
  9: "MEDIUMINT",
  10: "DATE",
  11: "TIME",
  12: "DATETIME",
  13: "YEAR",
  14: "DATE",
  15: "VARCHAR",
  16: "BIT",
  245: "JSON",
  246: "DECIMAL",
  247: "ENUM",
  248: "SET",
  249: "TINYTEXT/BLOB",
  250: "MEDIUMTEXT/BLOB",
  251: "LONGTEXT/BLOB",
  252: "TEXT/BLOB",
  253: "VARCHAR",
  254: "CHAR",
  255: "GEOMETRY",
};

const NUMERIC_TYPES = new Set([0, 1, 2, 3, 4, 5, 8, 9, 13, 246]);

export function columnTypeName(type: number | null): string {
  return type === null ? "" : TYPE_NAMES[type] || "type " + type;
}

export function isNumericType(type: number | null): boolean {
  return type !== null && NUMERIC_TYPES.has(type);
}

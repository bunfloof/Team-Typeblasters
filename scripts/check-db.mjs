// Checks the database settings in .env.local: connects and prints the server
// version and table sizes (never the password).   node scripts/check-db.mjs
import { readFileSync } from "node:fs";
import mysql from "mysql2/promise";

const env = {};
for (const line of readFileSync(new URL("../.env.local", import.meta.url), "utf8").split(/\r?\n/)) {
  const match = line.match(/^\s*([A-Z_]+)\s*=\s*(.*)\s*$/);
  if (match) {
    env[match[1]] = match[2].replace(/^'(.*)'$/, "$1").replace(/^"(.*)"$/, "$1");
  }
}
if (!env.DB_PASSWORD) {
  console.log("DB_PASSWORD is empty in .env.local");
  process.exit(2);
}
try {
  const connection = await mysql.createConnection({
    host: env.DB_HOST,
    port: Number(env.DB_PORT || 3306),
    user: env.DB_USER,
    password: env.DB_PASSWORD,
    database: env.DB_NAME,
    connectTimeout: 10000,
  });
  const [[version]] = await connection.query("SELECT VERSION() AS v");
  const [[events]] = await connection.query("SELECT COUNT(*) AS n FROM events");
  const [[requests]] = await connection.query("SELECT COUNT(*) AS n FROM requests");
  console.log("Connected: " + version.v + " | events: " + events.n + " | requests: " + requests.n);
  await connection.end();
} catch (error) {
  console.log("Connection failed: " + error.code + " " + error.message);
  process.exit(1);
}

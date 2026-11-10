import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  /* config options here */
  output: "standalone",
  cacheComponents: true,
  partialPrefetching: true,
  // `pnpm dev` only serves its scripts to pages opened at localhost unless the
  // address is listed here; without this, opening the dashboard at a LAN or
  // server address (http://192.168.8.100:3000) stays on "Loading...".
  // (`pnpm build` + `pnpm start` has no such check.) Entries are host names
  // without the port: "*" is one part of the name.
  allowedDevOrigins: [
    "192.168.8.100", // this PC on the Wi-Fi
    "216.219.90.186", // the server
    "*.*.*.*", // any other IPv4 address
    "**.*", // any domain name
  ],
  turbopack: {
    rules: {
      "*.css": {
        loaders: ["@tailwindcss/turbopack"],
        as: "*.css",
      },
    },
  },
};

export default nextConfig;

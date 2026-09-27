import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  // Hide the Next.js dev tools badge; compile/runtime errors still show.
  devIndicators: false,
  // Docker: build a self-contained server in .next/standalone (see Dockerfile). No effect on `npm run dev`.
  output: "standalone",
};

export default nextConfig;

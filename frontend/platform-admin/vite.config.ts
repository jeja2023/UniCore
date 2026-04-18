import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

export default defineConfig({
  plugins: [react()],
  build: {
    target: "es2022",
    sourcemap: true,
    chunkSizeWarningLimit: 900,
    rollupOptions: {
      output: {
        manualChunks(id) {
          if (id.includes("node_modules")) {
            if (id.includes("react-router")) {
              return "vendor-router";
            }
            if (id.includes("@tanstack")) {
              return "vendor-query";
            }
            if (id.includes("antd")) {
              return "vendor-antd";
            }
            if (id.includes("react-dom") || id.includes("/react/")) {
              return "vendor-react";
            }
            return "vendor";
          }
          return undefined;
        },
      },
    },
  },
  server: {
    port: 5173,
    proxy: {
      "/api": {
        target: "http://localhost:5000",
        changeOrigin: true,
      },
      "/swagger": {
        target: "http://localhost:5000",
        changeOrigin: true,
      },
    },
  },
});


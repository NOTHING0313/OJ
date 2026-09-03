import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    proxy: {
      "/api": {
        target: "http://localhost:5101",
        changeOrigin: true,
        headers: { "X-Forwarded-Proto": "https" }
      },
      "/uploads": {
        target: "http://localhost:5101",
        changeOrigin: true,
        headers: { "X-Forwarded-Proto": "https" }
      },
      "/theme-assets": {
        target: "http://localhost:5101",
        changeOrigin: true,
        headers: { "X-Forwarded-Proto": "https" }
      }
    }
  }
});

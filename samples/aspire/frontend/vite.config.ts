import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

// The Vite dev server receives its port from Aspire through the PORT environment
// variable. InertiaCore reads the dev server URL from the Vite__HotUrl app setting,
// which the AppHost injects from this resource's endpoint.
export default defineConfig({
    plugins: [react()],
    server: {
        host: "127.0.0.1",
        port: Number(process.env.PORT ?? 5173),
    },
    build: {
        // Emit the manifest the InertiaCore Vite helper reads at runtime.
        manifest: true,
        outDir: "dist",
    },
});

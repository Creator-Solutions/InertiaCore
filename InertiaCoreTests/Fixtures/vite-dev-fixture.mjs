// Minimal stand-in for `npm run dev` used by the Vite dev server integration test (IN-71).
// It writes the hot file (the same way Vite does) pointing at its own URL, records its pid
// in a marker file so the test can prove which process ran, and serves until it is killed.
import http from "node:http";
import fs from "node:fs";
import path from "node:path";

const port = Number(process.env.VITE_DEV_PORT ?? "5173");
const hotFile = process.env.VITE_HOT_FILE;
const marker = process.env.VITE_DEV_MARKER;

const url = `http://127.0.0.1:${port}`;

const server = http.createServer((req, res) => {
    res.writeHead(200, { "content-type": "text/html" });
    res.end("<html><body>vite-fixture</body></html>");
});

server.listen(port, "127.0.0.1", () => {
    if (hotFile) {
        fs.mkdirSync(path.dirname(hotFile), { recursive: true });
        fs.writeFileSync(hotFile, url);
    }
    if (marker) {
        fs.writeFileSync(marker, String(process.pid));
    }
    console.log(`vite fixture listening on ${url}`);
});


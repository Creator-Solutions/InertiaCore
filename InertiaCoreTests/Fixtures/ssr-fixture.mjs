// Minimal Inertia SSR fixture server used by the managed-SSR integration test (IN-68).
// It implements /health, /render and /shutdown but does not render Inertia pages; the test
// only asserts the .NET side launches, health-checks, forwards logs and shuts the process down
// with no orphans. Writes a per-instance marker file so the test can prove which process ran.
import http from "node:http";
import fs from "node:fs";

const port = Number(process.env.SSR_PORT ?? "13714");
const marker = process.env.SSR_MARKER;

if (marker) {
    fs.writeFileSync(marker, String(process.pid));
}

const server = http.createServer((req, res) => {
    if (req.url === "/health") {
        res.writeHead(200, { "content-type": "application/json" });
        res.end("{\"status\":\"ok\"}");
        return;
    }

    if (req.url === "/shutdown") {
        res.writeHead(200);
        res.end("ok");
        console.log("fixture received /shutdown");
        server.close(() => process.exit(0));
        return;
    }

    if (req.url === "/render") {
        res.writeHead(200, { "content-type": "application/json" });
        res.end("{\"head\":[],\"body\":\"<div>fixture</div>\"}");
        return;
    }

    res.writeHead(404);
    res.end();
});

server.listen(port, "127.0.0.1", () => {
    console.log(`fixture listening on ${port}`);
});

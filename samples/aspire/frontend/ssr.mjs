// Inertia server-side rendering entry for the Aspire sample.
//
// Aspire assigns this process a free port and injects it as PORT. The gateway
// (InertiaCore) polls /health before sending renders, posts the page to /render and
// asks for a graceful exit on /shutdown.
import http from "node:http";
import { createInertiaApp } from "@inertiajs/react";
import { renderToString } from "react-dom/server";
import React from "react";

const port = Number(process.env.PORT ?? process.env.SSR_PORT ?? 13714);
const pages = {
    Home: (await import("./src/Pages/Home.tsx")).default,
};

async function renderPage(page) {
    return createInertiaApp({
        page,
        render: renderToString,
        resolve: (name) => pages[name],
        setup: ({ App, props }) => React.createElement(App, props),
    });
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
        server.close(() => process.exit(0));
        return;
    }

    if (req.url === "/render") {
        let body = "";
        req.on("data", (chunk) => (body += chunk));
        req.on("end", async () => {
            try {
                const page = JSON.parse(body);
                const result = await renderPage(page);
                res.writeHead(200, { "content-type": "application/json" });
                res.end(JSON.stringify(result));
            } catch (error) {
                console.error(error);
                res.writeHead(500, { "content-type": "application/json" });
                res.end(JSON.stringify({ error: String(error) }));
            }
        });
        return;
    }

    res.writeHead(404);
    res.end();
});

server.listen(port, "127.0.0.1", () => {
    console.log(`SSR server listening on ${port}`);
});

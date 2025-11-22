const http = require('http');
const fs = require('fs');
const path = require('path');
const url = require('url');

const PORT = 3000;
const UNITY_PORT = 8080;
const DOCS_DIR = path.join(__dirname, '../docs/design/UIUX');

const server = http.createServer((req, res) => {
    const parsedUrl = url.parse(req.url, true);
    
    // API Proxy to Unity
    if (parsedUrl.pathname.startsWith('/api/')) {
        const unityPath = parsedUrl.pathname.replace('/api', '');
        const options = {
            hostname: 'localhost',
            port: UNITY_PORT,
            path: unityPath,
            method: req.method,
            headers: req.headers
        };

        const proxyReq = http.request(options, (proxyRes) => {
            res.writeHead(proxyRes.statusCode, proxyRes.headers);
            proxyRes.pipe(res, { end: true });
        });

        proxyReq.on('error', (e) => {
            console.error(`[Proxy Error] Is Unity running? ${e.message}`);
            res.writeHead(502, { 'Content-Type': 'application/json' });
            res.end(JSON.stringify({ error: 'Unity Connection Failed', details: e.message }));
        });

        req.pipe(proxyReq, { end: true });
        return;
    }

    // Static File Serving
    let filePath = path.join(DOCS_DIR, parsedUrl.pathname === '/' ? 'dashboard_mockup.html' : parsedUrl.pathname);
    
    // Map specific URLs to mockups for convenience
    if (parsedUrl.pathname === '/dashboard') filePath = path.join(DOCS_DIR, 'dashboard_mockup.html');
    if (parsedUrl.pathname === '/new_session') filePath = path.join(DOCS_DIR, 'new_session_mockup.html');
    if (parsedUrl.pathname === '/session_detail') filePath = path.join(DOCS_DIR, 'session_detail_mockup.html');

    const extname = path.extname(filePath);
    let contentType = 'text/html';
    switch (extname) {
        case '.js': contentType = 'text/javascript'; break;
        case '.css': contentType = 'text/css'; break;
        case '.json': contentType = 'application/json'; break;
        case '.png': contentType = 'image/png'; break;
        case '.jpg': contentType = 'image/jpg'; break;
    }

    fs.readFile(filePath, (error, content) => {
        if (error) {
            if(error.code == 'ENOENT'){
                res.writeHead(404);
                res.end('File not found: ' + filePath);
            } else {
                res.writeHead(500);
                res.end('Sorry, check with the site admin for error: '+error.code+' ..\n');
            }
        } else {
            res.writeHead(200, { 'Content-Type': contentType });
            res.end(content, 'utf-8');
        }
    });
});

server.listen(PORT, () => {
    console.log(`Web Dashboard running at http://localhost:${PORT}/`);
    console.log(`Serving files from: ${DOCS_DIR}`);
    console.log(`Proxying /api/* to Unity at http://localhost:${UNITY_PORT}/`);
});

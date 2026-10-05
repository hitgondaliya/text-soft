// pm2 process definition. `pm2 start ecosystem.config.cjs` (deploy.sh does this).
// Next.js reads .env itself on start; only the listening port is decided here,
// because `next start -p` needs it before the app loads.
//
// PORT: from the environment, else the PORT= line of the .env next to this file,
// else 3100. nginx must proxy to the same port (see nginx.conf.example).
// pm2 keeps the arguments it was started with — after changing PORT run
// `pm2 delete textsoft-admin && pm2 start ecosystem.config.cjs` (deploy.sh does).
const fs = require("node:fs");
const path = require("node:path");

function portFromDotEnv() {
  try {
    const text = fs.readFileSync(path.join(__dirname, ".env"), "utf8");
    let port = "";
    for (const line of text.split(/\r?\n/)) {
      const m = /^\s*(?:export\s+)?PORT\s*=\s*["']?(\d+)["']?\s*(?:#.*)?$/.exec(line);
      if (m) port = m[1];
    }
    return port;
  } catch {
    return "";
  }
}

const PORT = String(process.env.PORT || portFromDotEnv() || "3100").trim();
// Listen on loopback only: nginx is the one public entry (HTTPS, real client IP in
// X-Real-IP). Without -H, `next start` would also answer on http://<vps-ip>:PORT.
const BIND_HOST = String(process.env.BIND_HOST || "127.0.0.1").trim();

module.exports = {
  apps: [
    {
      name: process.env.APP_NAME || "textsoft-admin",
      script: "node_modules/next/dist/bin/next",
      args: "start -H " + BIND_HOST + " -p " + PORT,
      cwd: __dirname,
      instances: 1, // the in-process rate limiter assumes one instance
      exec_mode: "fork",
      autorestart: true,
      max_memory_restart: "400M",
      env: { NODE_ENV: "production", PORT },
      out_file: "./logs/out.log",
      error_file: "./logs/err.log",
      time: true,
    },
  ],
};

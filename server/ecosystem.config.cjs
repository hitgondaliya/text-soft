// pm2 process definition. `pm2 start ecosystem.config.cjs` (deploy.sh does this).
// Env comes from .env, which Next.js reads itself on start; only PORT is set here.
module.exports = {
  apps: [
    {
      name: "textsoft-admin",
      script: "node_modules/next/dist/bin/next",
      args: "start -p " + (process.env.PORT || "3000"),
      cwd: __dirname,
      instances: 1, // the in-process rate limiter assumes one instance
      exec_mode: "fork",
      autorestart: true,
      max_memory_restart: "400M",
      env: { NODE_ENV: "production" },
      out_file: "./logs/out.log",
      error_file: "./logs/err.log",
      time: true,
    },
  ],
};

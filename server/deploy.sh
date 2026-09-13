#!/usr/bin/env bash
# Deploy / update the TextSoft licence panel on the VPS.
#
#   First time:   unzip textsoft-admin.zip -d /var/www && cd /var/www/textsoft-admin && ./deploy.sh
#   Update:       upload the new zip, unzip over the folder (keep .env), ./deploy.sh
#
# What it does: installs deps with pnpm, builds Next.js, seeds/updates the bootstrap
# admin from .env, and (re)starts the app under pm2 as "textsoft-admin" on $PORT.
# nginx in front of it terminates HTTPS for admin.text-soft.com (see nginx.conf.example).

set -euo pipefail
cd "$(dirname "$0")"

APP_NAME="${APP_NAME:-textsoft-admin}"
PORT="${PORT:-3000}"

if [ ! -f .env ]; then
  echo "!! .env is missing. Copy .env.production to .env and fill in the secrets first." >&2
  exit 1
fi

# pnpm (via corepack when available, else global)
if ! command -v pnpm >/dev/null 2>&1; then
  if command -v corepack >/dev/null 2>&1; then
    corepack enable && corepack prepare pnpm@latest --activate
  else
    npm install -g pnpm
  fi
fi

echo "== Installing dependencies =="
# mongodb-memory-server is dev-only (local runs); production never downloads a mongod.
MONGOMS_DISABLE_POSTINSTALL=1 pnpm install --frozen-lockfile --prod=false

echo "== Building =="
pnpm run build

echo "== Seeding the bootstrap admin from .env =="
node --env-file=.env scripts/seed-admin.mjs

echo "== (Re)starting under pm2 =="
chmod 600 .env
if pm2 describe "$APP_NAME" >/dev/null 2>&1; then
  pm2 restart "$APP_NAME" --update-env
else
  PORT="$PORT" pm2 start ecosystem.config.cjs
fi
pm2 save

echo
echo "Done. App is on http://127.0.0.1:$PORT — nginx should proxy admin.text-soft.com to it."
echo "Logs: pm2 logs $APP_NAME"

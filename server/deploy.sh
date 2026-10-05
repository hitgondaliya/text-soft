#!/usr/bin/env bash
# Deploy / update the TextSoft licence panel on the VPS.
#
#   First time:   unzip textsoft-admin.zip -d /var/www && cd /var/www/textsoft-admin
#                 cp .env.production.example .env && nano .env && ./deploy.sh
#   Update:       unzip -o ~/textsoft-admin.zip -x 'textsoft-admin/.env' 'textsoft-admin/.env.*' -d /var/www
#                 (never lets the zip touch .env), then ./deploy.sh
#
# What it does: installs deps with pnpm, builds Next.js, seeds/updates the bootstrap
# admin from .env, and (re)starts the app under pm2 as "textsoft-admin" on $PORT.
# nginx in front of it terminates HTTPS for admin.text-soft.com (see nginx.conf.example).
#
# PORT: from the environment, else the PORT= line in .env, else 3100.

set -euo pipefail
cd "$(dirname "$0")"

APP_NAME="${APP_NAME:-textsoft-admin}"

if [ ! -f .env ]; then
  echo "!! .env is missing. Copy .env.production.example to .env and fill in the secrets first." >&2
  exit 1
fi

# `next start` reads .env.production.local, .env.local and .env.production BEFORE .env,
# and the first file that names a variable wins — even with an empty value. A stray
# template or dev file would blank COOKIE_SECRET / LICENSE_PRIVATE_KEY / KEY_VAULT_SECRET
# (nobody can sign in, every activation fails) or swap in dev keys. Only .env may exist.
for f in .env.production .env.local .env.production.local; do
  if [ -f "$f" ]; then
    echo "!! $f is here. Next.js reads it before .env and it overrides .env's values." >&2
    echo "   Move anything you need from it into .env, then remove it:  rm $f" >&2
    exit 1
  fi
done

# Last NAME=value line of .env, without quotes, a trailing comment or a Windows CR.
env_value() {
  grep -E "^[[:space:]]*(export[[:space:]]+)?$1[[:space:]]*=" .env 2>/dev/null | tail -n 1 \
    | sed -E 's/^[^=]*=[[:space:]]*//; s/\r$//; s/[[:space:]]+#.*$//; s/^["'"'"']//; s/["'"'"']$//' || true
}

PORT="${PORT:-$(env_value PORT)}"
PORT="${PORT:-3100}"
case "$PORT" in
  ''|*[!0-9]*) echo "!! PORT must be a number (got '$PORT'). Fix PORT= in .env." >&2; exit 1 ;;
esac
export PORT APP_NAME

if [ -z "$(env_value KEY_VAULT_SECRET)" ]; then
  echo "!! KEY_VAULT_SECRET is not set in .env."
  echo "   Licence keys and the SMTP password are then sealed with a key derived from COOKIE_SECRET,"
  echo "   and changing COOKIE_SECRET later would make them unreadable. Add it once and never change it:"
  echo "     echo \"KEY_VAULT_SECRET=\$(openssl rand -base64 32)\" >> .env && ./deploy.sh"
  echo
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

echo "== Checking the vault secrets against the database =="
# Stops here (the running app stays up) if this .env cannot open the keys and SMTP
# password already stored — e.g. an update replaced .env and KEY_VAULT_SECRET was lost.
node --env-file=.env scripts/vault-check.mjs

echo "== Seeding the bootstrap admin from .env =="
node --env-file=.env scripts/seed-admin.mjs

echo "== (Re)starting under pm2 on 127.0.0.1:$PORT =="
chmod 600 .env
mkdir -p logs
# `pm2 restart` keeps the arguments the app was first started with (the old -p port),
# so drop the old entry and start it fresh — a PORT change then takes effect.
if pm2 describe "$APP_NAME" >/dev/null 2>&1; then
  pm2 delete "$APP_NAME" >/dev/null
fi
pm2 start ecosystem.config.cjs
pm2 save

# Wait for it to answer locally.
if command -v curl >/dev/null 2>&1; then
  up=""
  for _ in $(seq 1 30); do
    if curl -fsS -o /dev/null "http://127.0.0.1:$PORT/login"; then up=1; break; fi
    sleep 1
  done
  if [ -n "$up" ]; then
    echo "   OK: the panel answers on http://127.0.0.1:$PORT"
  else
    echo "!! The panel did not answer on http://127.0.0.1:$PORT within 30 s — check: pm2 logs $APP_NAME" >&2
  fi
fi

echo
echo "Done. App is on http://127.0.0.1:$PORT"
echo "Logs: pm2 logs $APP_NAME"
echo
echo ">> nginx must proxy admin.text-soft.com to 127.0.0.1:$PORT  (proxy_pass http://127.0.0.1:$PORT;)"
NGINX_SITE="/etc/nginx/sites-enabled/textsoft-admin"
if [ -r "$NGINX_SITE" ]; then
  if grep -Eq "proxy_pass[[:space:]]+http://(127\.0\.0\.1|localhost):$PORT[;/[:space:]]" "$NGINX_SITE"; then
    echo "   nginx: $NGINX_SITE already proxies to port $PORT."
  else
    echo "!! nginx: $NGINX_SITE does NOT proxy to port $PORT. Change its proxy_pass to"
    echo "     proxy_pass http://127.0.0.1:$PORT;"
    echo "   then: sudo nginx -t && sudo systemctl reload nginx"
  fi
else
  echo "   After changing the port: edit proxy_pass in your nginx site, then sudo nginx -t && sudo systemctl reload nginx"
fi

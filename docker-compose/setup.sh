#!/usr/bin/env bash
# Creates .env from .env.example with random secrets (run once before the first start).
set -euo pipefail
cd "$(dirname "$0")"

if [[ -f .env ]]; then
  echo ".env already exists - nothing changed."
  exit 0
fi

random() { openssl rand -hex "$1" 2>/dev/null || head -c "$1" /dev/urandom | od -An -tx1 | tr -d ' \n'; }

cp .env.example .env
chmod 600 .env
sed -i "s|^POSTGRES_PASSWORD=.*|POSTGRES_PASSWORD=$(random 24)|" .env
sed -i "s|^JWT_KEY=.*|JWT_KEY=$(random 48)|" .env

read -r -p "Initial admin user name [admin]: " admin_user
admin_user=${admin_user:-admin}
read -r -s -p "Initial admin password (empty = create on setup page, min. 12 chars): " admin_pw
echo
sed -i "s|^ADMIN_USERNAME=.*|ADMIN_USERNAME=${admin_user}|" .env
if [[ -n "$admin_pw" ]]; then
  # escape characters that are special for sed
  escaped=$(printf '%s' "$admin_pw" | sed -e 's/[\/&|]/\\&/g')
  sed -i "s|^ADMIN_PASSWORD=.*|ADMIN_PASSWORD=${escaped}|" .env
fi

echo ".env created. Review it (SMTP, LDAP, MONITORER_ALLOW_HTTP), then start with:"
echo "  docker compose up -d --build"
echo "After the first login you can remove ADMIN_PASSWORD from .env."

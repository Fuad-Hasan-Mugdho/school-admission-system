#!/bin/sh
# Runs automatically by the official nginx image before nginx starts.
# Writes the backend URL from the API_BASE_URL environment variable into config.js,
# which index.html loads before the app bundle.
set -eu
API_BASE_URL="${API_BASE_URL:-http://localhost:8080}"
# Escape characters that would break a JS string literal.
ESCAPED=$(printf '%s' "$API_BASE_URL" | sed 's/\\/\\\\/g; s/"/\\"/g')
cat > /usr/share/nginx/html/config.js <<JS
// Generated at container start from API_BASE_URL.
window.__APP_CONFIG__ = { apiBaseUrl: "${ESCAPED}" };
JS
echo "[frontend] API base URL: ${API_BASE_URL}"

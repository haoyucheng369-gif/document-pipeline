#!/bin/sh
set -eu

export RUNTIME_API_BASE_URL="${RUNTIME_API_BASE_URL:-http://localhost:8080}"
export RUNTIME_APP_ENV="${RUNTIME_APP_ENV:-development}"

envsubst '${RUNTIME_API_BASE_URL} ${RUNTIME_APP_ENV}' \
  < /usr/share/nginx/html/config.js.template \
  > /usr/share/nginx/html/config.js

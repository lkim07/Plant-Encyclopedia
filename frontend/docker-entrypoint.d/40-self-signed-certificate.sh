#!/bin/sh
# A throwaway self-signed certificate for localhost, made on first start when none is mounted (design 2026-10-27).
# Only for running the app on this machine or a CI runner; never for a public site.
set -e
dir=/etc/nginx/certs
if [ ! -f "$dir/site.crt" ]; then
  mkdir -p "$dir"
  openssl req -x509 -newkey rsa:2048 -nodes -days 7 -subj "/CN=localhost" \
    -addext "subjectAltName=DNS:localhost,IP:127.0.0.1" \
    -keyout "$dir/site.key" -out "$dir/site.crt" 2>/dev/null
  echo "Made a throwaway self-signed certificate for localhost"
fi

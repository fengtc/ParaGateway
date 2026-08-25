#!/usr/bin/env bash
set -euo pipefail
release=${1:?usage: promote.sh <release>}
test -s "/opt/paragateway/releases/$release/backend/paragateway-backend"
test -s "/opt/paragateway/releases/$release/frontend/wwwroot/index.html"
systemctl is-active --quiet "paragateway-backend@$release.service"
systemctl is-active --quiet "paragateway-gateway@$release.service"
systemctl stop paragateway-gateway.service paragateway-backend.service || true
sed 's/8284/8184/g; s/8282/8182/g' "/etc/paragateway/$release/Caddyfile" > "/etc/paragateway/$release/Caddyfile.production"
sed "s#%i#$release#g; s#8284#8184#g" /etc/systemd/system/paragateway-backend@.service > /etc/systemd/system/paragateway-backend.service
sed "s#%i#$release#g; s#Caddyfile#Caddyfile.production#g" /etc/systemd/system/paragateway-gateway@.service > /etc/systemd/system/paragateway-gateway.service
systemctl daemon-reload
systemctl restart paragateway-backend.service paragateway-gateway.service
curl -fsS --max-time 10 https://api.blsc.dev/health
echo "promoted: $release"

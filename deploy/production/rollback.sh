#!/usr/bin/env bash
set -euo pipefail
release=${1:?usage: rollback.sh <previous-release>}
test -s "/opt/paragateway/releases/$release/backend/paragateway-backend"
systemctl stop paragateway-gateway.service paragateway-backend.service || true
sed "s#%i#$release#g; s#8284#8184#g" /etc/systemd/system/paragateway-backend@.service > /etc/systemd/system/paragateway-backend.service
sed "s#%i#$release#g; s#8282#8182#g" /etc/systemd/system/paragateway-gateway@.service > /etc/systemd/system/paragateway-gateway.service
systemctl daemon-reload
systemctl restart paragateway-backend.service paragateway-gateway.service
curl -fsS --max-time 10 https://api.blsc.dev/health
echo "rolled back: $release"

#!/usr/bin/env bash
set -euo pipefail
release=${1:?usage: verify-candidate.sh <release>}
curl -fsS --max-time 10 http://127.0.0.1:8284/health
curl -fsS --max-time 10 http://127.0.0.1:8282/health
curl -fsS --max-time 10 http://127.0.0.1:8282/setup/status
curl -fsS --max-time 10 http://127.0.0.1:8282/ -o /dev/null
systemctl is-active --quiet "paragateway-backend@$release.service"
systemctl is-active --quiet "paragateway-gateway@$release.service"
if journalctl -u "paragateway-backend@$release.service" -u "paragateway-gateway@$release.service" --since=-5min -p err --no-pager | grep -q .; then exit 1; fi
echo "candidate verified: $release"

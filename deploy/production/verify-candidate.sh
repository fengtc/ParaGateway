#!/usr/bin/env bash
set -euo pipefail
release=${1:?usage: verify-candidate.sh <release>}
[[ "$release" =~ ^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$ ]] || { echo 'invalid release name' >&2; exit 1; }
test -s "/etc/paragateway/$release/candidate.env"
test -s "/etc/paragateway/$release/production.env"
test -s "/etc/paragateway/$release/production-env-source"
test -s "/etc/paragateway/$release/production-config-source"
test -s "/etc/paragateway/$release/production-commit"
test -s "/etc/paragateway/$release/target-commit"
test -s "/etc/paragateway/$release/Caddyfile.production"
test -s "/etc/paragateway/$release/paragateway-backend.service"
test -s "/etc/paragateway/$release/paragateway-gateway.service"
curl -fsS --max-time 10 http://127.0.0.1:8284/health
curl -fsS --max-time 10 http://127.0.0.1:8282/health
curl -fsS --max-time 10 http://127.0.0.1:8282/setup/status
curl -fsS --max-time 10 http://127.0.0.1:8282/ -o /dev/null
expected_commit=$(tr -d '\r\n' < "/etc/paragateway/$release/target-commit")
served_commit=$(curl -fsS --max-time 10 http://127.0.0.1:8282/release-commit.txt | tr -d '\r\n')
[ "$served_commit" = "$expected_commit" ] || { echo 'candidate frontend commit identity mismatch' >&2; exit 1; }
for path in /v1/models /responses /messages; do
  content_type=$(curl -sS --max-time 10 -o /dev/null -w '%{content_type}' "http://127.0.0.1:8282$path")
  case "$content_type" in text/html*) echo "backend route $path fell through to the SPA" >&2; exit 1 ;; esac
done
systemctl is-active --quiet "paragateway-backend@$release.service"
systemctl is-active --quiet "paragateway-gateway@$release.service"
grep -Fqx 'Environment=SERVER_TRUSTED_PROXIES=127.0.0.1/32,::1/128' /etc/systemd/system/paragateway-backend@.service
grep -Fqx 'EnvironmentFile=/etc/paragateway/%i/candidate.env' /etc/systemd/system/paragateway-backend@.service
grep -Fq 'trusted_proxies 127.0.0.1/32' "/etc/paragateway/$release/Caddyfile"
grep -Fq 'trusted_proxies 127.0.0.1/32' "/etc/paragateway/$release/Caddyfile.production"
sudo -u sub2api /usr/bin/caddy validate --config "/etc/paragateway/$release/Caddyfile.production" --adapter caddyfile
systemd-analyze verify "/etc/paragateway/$release/paragateway-backend.service" "/etc/paragateway/$release/paragateway-gateway.service"
if journalctl --quiet -u "paragateway-backend@$release.service" -u "paragateway-gateway@$release.service" --since=-5min -p err --no-pager | grep -q .; then exit 1; fi
echo "candidate verified: $release"

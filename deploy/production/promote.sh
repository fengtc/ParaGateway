#!/usr/bin/env bash
set -Eeuo pipefail
release=${1:?usage: promote.sh <release>}
production_env=${PRODUCTION_ENV_FILE:?set PRODUCTION_ENV_FILE to the current production environment file}
production_config=${PRODUCTION_CONFIG_FILE:?set PRODUCTION_CONFIG_FILE to the current production config file}
[[ "$release" =~ ^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$ ]] || { echo 'invalid release name' >&2; exit 1; }
command -v flock >/dev/null || { echo 'flock is required' >&2; exit 1; }
exec 9>/run/lock/paragateway-release.lock
flock -n 9 || { echo 'another ParaGateway release operation is running' >&2; exit 1; }
pointer="/etc/paragateway/$release/previous-production-snapshot"
[ ! -e "$pointer" ] || { echo 'promotion was already attempted for this release' >&2; exit 1; }
wait_for_health() {
  local attempt
  for attempt in {1..30}; do
    if curl -fsS --max-time 5 http://127.0.0.1:8184/health >/dev/null \
      && curl -fsS --max-time 5 http://127.0.0.1:8182/health >/dev/null \
      && curl -fsS --max-time 5 https://api.blsc.dev/health >/dev/null; then return 0; fi
    sleep 1
  done
  return 1
}
test -s "/opt/paragateway/releases/$release/backend/paragateway-backend"
test -s "/opt/paragateway/releases/$release/frontend/wwwroot/index.html"
test -s "/etc/paragateway/$release/production.env"
test -s "/etc/paragateway/$release/production-env-source"
test -s "/etc/paragateway/$release/production-config-source"
test -s "/etc/paragateway/$release/production-commit"
test -s "/etc/paragateway/$release/target-commit"
test -s "/etc/paragateway/$release/Caddyfile.production"
test -s "/etc/paragateway/$release/paragateway-backend.service"
test -s "/etc/paragateway/$release/paragateway-gateway.service"
systemctl is-active --quiet "paragateway-backend@$release.service"
systemctl is-active --quiet "paragateway-gateway@$release.service"
bash "$(dirname "$0")/verify-candidate.sh" "$release"
systemctl is-active --quiet paragateway-backend.service
systemctl is-active --quiet paragateway-gateway.service
[ -z "$(systemctl show -p DropInPaths --value paragateway-backend.service)" ] || { echo 'production backend has unsupported systemd drop-ins' >&2; exit 1; }
[ -z "$(systemctl show -p DropInPaths --value paragateway-gateway.service)" ] || { echo 'production gateway has unsupported systemd drop-ins' >&2; exit 1; }
production_environment_files=$(systemctl show -p EnvironmentFiles --value paragateway-backend.service)
if [[ "$production_environment_files" =~ ^([^[:space:]]+)[[:space:]]+\(ignore_errors=(yes|no)\)$ ]]; then
  active_production_env=${BASH_REMATCH[1]}
else
  echo 'production backend must use exactly one EnvironmentFile' >&2
  exit 1
fi
production_fragment=$(systemctl show -p FragmentPath --value paragateway-backend.service)
test -s "$production_fragment"
mapfile -t production_config_entries < <(
  sed -nE 's/^[[:space:]]*LoadCredential[[:space:]]*=[[:space:]]*config\.yaml:([^[:space:]#]+)[[:space:]]*$/\1/p' "$production_fragment"
)
[ "${#production_config_entries[@]}" -eq 1 ] || { echo 'production backend must use exactly one config.yaml credential' >&2; exit 1; }
active_production_config=${production_config_entries[0]}
test -s "$production_env"
test -s "$production_config"
production_env_real=$(readlink -f -- "$production_env")
production_config_real=$(readlink -f -- "$production_config")
active_production_env_real=$(readlink -f -- "$active_production_env")
active_production_config_real=$(readlink -f -- "$active_production_config")
recorded_production_env=$(tr -d '\r\n' < "/etc/paragateway/$release/production-env-source")
recorded_production_config=$(tr -d '\r\n' < "/etc/paragateway/$release/production-config-source")
[ "$production_env_real" = "$active_production_env_real" ] \
  && [ "$production_env_real" = "$recorded_production_env" ] \
  || { echo 'PRODUCTION_ENV_FILE is not used by the active production backend' >&2; exit 1; }
[ "$production_config_real" = "$active_production_config_real" ] \
  && [ "$production_config_real" = "$recorded_production_config" ] \
  || { echo 'PRODUCTION_CONFIG_FILE is not used by the active production backend' >&2; exit 1; }
cmp -s "$active_production_env_real" "/etc/paragateway/$release/production.env" || { echo 'production environment changed after candidate deployment' >&2; exit 1; }
cmp -s "$active_production_config_real" "/etc/paragateway/$release/config.yaml" || { echo 'production config changed after candidate deployment' >&2; exit 1; }
curl -fsS --max-time 10 https://api.blsc.dev/health >/dev/null
sudo -u sub2api /usr/bin/caddy validate --config "/etc/paragateway/$release/Caddyfile.production" --adapter caddyfile
systemd-analyze verify "/etc/paragateway/$release/paragateway-backend.service" "/etc/paragateway/$release/paragateway-gateway.service"
test -s /etc/systemd/system/paragateway-backend.service
test -s /etc/systemd/system/paragateway-gateway.service
snapshot="/etc/paragateway/rollback-snapshots/pre-$release-$(date -u +%Y%m%dT%H%M%SZ)"
install -d -o root -g root -m 700 "$snapshot"
install -o root -g root -m 644 /etc/systemd/system/paragateway-backend.service "$snapshot/paragateway-backend.service"
install -o root -g root -m 644 /etc/systemd/system/paragateway-gateway.service "$snapshot/paragateway-gateway.service"
printf '%s\n' "$snapshot" > "/etc/paragateway/$release/previous-production-snapshot"
chmod 600 "/etc/paragateway/$release/previous-production-snapshot"
restore_previous_production() {
  local original_status=$?
  trap - ERR
  set +e
  echo 'promotion failed; restoring previous production units' >&2
  install -o root -g root -m 644 "$snapshot/paragateway-backend.service" /etc/systemd/system/paragateway-backend.service
  install -o root -g root -m 644 "$snapshot/paragateway-gateway.service" /etc/systemd/system/paragateway-gateway.service
  systemctl daemon-reload
  systemctl restart paragateway-backend.service paragateway-gateway.service
  wait_for_health
  exit "$original_status"
}
trap restore_previous_production ERR
install -o root -g root -m 644 "/etc/paragateway/$release/paragateway-backend.service" /etc/systemd/system/paragateway-backend.service
install -o root -g root -m 644 "/etc/paragateway/$release/paragateway-gateway.service" /etc/systemd/system/paragateway-gateway.service
systemctl daemon-reload
systemctl restart paragateway-backend.service paragateway-gateway.service
wait_for_health
expected_commit=$(tr -d '\r\n' < "/etc/paragateway/$release/target-commit")
served_commit=$(curl -fsS --max-time 10 http://127.0.0.1:8182/release-commit.txt | tr -d '\r\n')
[ "$served_commit" = "$expected_commit" ] || { echo 'production frontend commit identity mismatch' >&2; false; }
trap - ERR
echo "promoted: $release"
echo "rollback snapshot: $snapshot"

#!/usr/bin/env bash
set -Eeuo pipefail
release=${1:?usage: rollback.sh <failed-release>}
[[ "$release" =~ ^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$ ]] || { echo 'invalid release name' >&2; exit 1; }
command -v flock >/dev/null || { echo 'flock is required' >&2; exit 1; }
exec 9>/run/lock/paragateway-release.lock
flock -n 9 || { echo 'another ParaGateway release operation is running' >&2; exit 1; }
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
pointer="/etc/paragateway/$release/previous-production-snapshot"
test -s "$pointer"
snapshot=$(cat "$pointer")
case "$snapshot" in
  /etc/paragateway/rollback-snapshots/*) ;;
  *) echo 'invalid rollback snapshot path' >&2; exit 1 ;;
esac
test -s "$snapshot/paragateway-backend.service"
test -s "$snapshot/paragateway-gateway.service"
systemd-analyze verify "$snapshot/paragateway-backend.service" "$snapshot/paragateway-gateway.service"
test -s /etc/systemd/system/paragateway-backend.service
test -s /etc/systemd/system/paragateway-gateway.service
rollback_guard="/etc/paragateway/rollback-snapshots/pre-rollback-$release-$(date -u +%Y%m%dT%H%M%SZ)"
install -d -o root -g root -m 700 "$rollback_guard"
install -o root -g root -m 644 /etc/systemd/system/paragateway-backend.service "$rollback_guard/paragateway-backend.service"
install -o root -g root -m 644 /etc/systemd/system/paragateway-gateway.service "$rollback_guard/paragateway-gateway.service"
restore_pre_rollback_production() {
  local original_status=$?
  trap - ERR
  set +e
  echo 'rollback failed; restoring the units active before rollback' >&2
  install -o root -g root -m 644 "$rollback_guard/paragateway-backend.service" /etc/systemd/system/paragateway-backend.service
  install -o root -g root -m 644 "$rollback_guard/paragateway-gateway.service" /etc/systemd/system/paragateway-gateway.service
  systemctl daemon-reload
  systemctl restart paragateway-backend.service paragateway-gateway.service
  if ! wait_for_health; then
    echo 'failed to restore healthy production after rollback failure' >&2
  fi
  exit "$original_status"
}
trap restore_pre_rollback_production ERR
install -o root -g root -m 644 "$snapshot/paragateway-backend.service" /etc/systemd/system/paragateway-backend.service
install -o root -g root -m 644 "$snapshot/paragateway-gateway.service" /etc/systemd/system/paragateway-gateway.service
systemctl daemon-reload
systemctl restart paragateway-backend.service paragateway-gateway.service
wait_for_health
trap - ERR
echo "rolled back from: $release"
echo "restored snapshot: $snapshot"
echo "pre-rollback safety snapshot: $rollback_guard"

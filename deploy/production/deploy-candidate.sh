#!/usr/bin/env bash
set -euo pipefail
commit=${1:?usage: deploy-candidate.sh <commit>}
source_dir=${SOURCE_DIR:-/opt/paragateway/source}
release=${RELEASE_NAME:-candidate-${commit:0:12}-$(date -u +%Y%m%dT%H%M%SZ)}
release_dir=/opt/paragateway/releases/$release
candidate_env=${CANDIDATE_ENV_FILE:?set CANDIDATE_ENV_FILE}
production_env=${PRODUCTION_ENV_FILE:-/etc/paragateway/production.env}
production_config=${PRODUCTION_CONFIG_FILE:-/etc/paragateway/production-config.yaml}
frontend_archive=${FRONTEND_ARCHIVE:?set FRONTEND_ARCHIVE}
expected_candidate_redis_db=${CANDIDATE_REDIS_DB_EXPECTED:-15}
test -f "$candidate_env" && test -f "$production_env" && test -f "$production_config" && test -f "$frontend_archive"
mkdir -p "$source_dir" /opt/paragateway/releases
if [ ! -d "$source_dir/.git" ]; then git clone https://github.com/fengtc/ParaGateway.git "$source_dir"; fi
git -C "$source_dir" fetch --prune origin
git -C "$source_dir" checkout --detach "$commit"
git -C "$source_dir" diff --quiet
source "$candidate_env"
: "${DATABASE_HOST:?candidate env must set DATABASE_HOST}"
: "${DATABASE_DBNAME:?candidate env must set DATABASE_DBNAME}"
: "${REDIS_DB:?candidate env must set REDIS_DB}"
candidate_database_host=$DATABASE_HOST
candidate_database_dbname=$DATABASE_DBNAME
candidate_redis_db=$REDIS_DB
[ "$candidate_redis_db" = "$expected_candidate_redis_db" ] || { echo "candidate REDIS_DB must be $expected_candidate_redis_db" >&2; exit 1; }
unset DATABASE_HOST DATABASE_DBNAME REDIS_DB
source "$production_env"
: "${DATABASE_HOST:?production env must set DATABASE_HOST}"
: "${DATABASE_DBNAME:?production env must set DATABASE_DBNAME}"
production_database_host=$DATABASE_HOST
production_database_dbname=$DATABASE_DBNAME
production_redis_db=${REDIS_DB:-0}
if [ "$candidate_database_host" = "$production_database_host" ] && [ "$candidate_database_dbname" = "$production_database_dbname" ]; then
  echo 'candidate DB target equals production' >&2
  exit 1
fi
[ "$candidate_redis_db" != "$production_redis_db" ] || { echo 'candidate REDIS_DB equals production' >&2; exit 1; }
tmp=$(mktemp)
dropin_tmp=$(mktemp)
data_env_tmp=$(mktemp)
trap 'rm -f "$tmp" "$dropin_tmp" "$data_env_tmp"' EXIT
(cd "$source_dir/backend" && GOOS=linux GOARCH=amd64 CGO_ENABLED=0 go build -buildvcs=false -trimpath -ldflags='-s -w' -o "$tmp" ./cmd/server)
install -d -o sub2api -g sub2api -m 750 "$release_dir/backend" "$release_dir/frontend"
install -o sub2api -g sub2api -m 750 "$tmp" "$release_dir/backend/paragateway-backend"
tar -xzf "$frontend_archive" -C "$release_dir/frontend"
test -s "$release_dir/frontend/wwwroot/index.html"
install -d -o root -g sub2api -m 750 "/etc/paragateway/$release"
install -o root -g root -m 600 "$candidate_env" "/etc/paragateway/$release/backend.env"
install -o root -g sub2api -m 640 "$production_config" "/etc/paragateway/$release/config.yaml"
cp "$(dirname "$0")/production.Candidate.Caddyfile.example" "/etc/paragateway/$release/Caddyfile"
sed -i "s#127.0.0.1:8184#127.0.0.1:8284#g; s#127.0.0.1:8182#127.0.0.1:8282#g; s#%RELEASE_ROOT%#$release_dir/frontend/wwwroot#g" "/etc/paragateway/$release/Caddyfile"
install -o root -g root -m 644 "$(dirname "$0")/paragateway-backend@.service" /etc/systemd/system/
install -o root -g root -m 644 "$(dirname "$0")/paragateway-gateway@.service" /etc/systemd/system/
dropin_dir="/etc/systemd/system/paragateway-backend@$release.service.d"
install -d -o root -g root -m 755 "$dropin_dir"
printf 'DATA_DIR=/var/lib/paragateway-backend-%s\n' "$release" > "$data_env_tmp"
install -o root -g root -m 600 "$data_env_tmp" "/etc/paragateway/$release/data.env"
printf '[Service]\nEnvironmentFile=/etc/paragateway/%s/data.env\nReadWritePaths=\nReadWritePaths=/var/lib/paragateway-backend-%s\n' "$release" "$release" > "$dropin_tmp"
install -o root -g root -m 644 "$dropin_tmp" "$dropin_dir/data.conf"
systemctl daemon-reload
systemctl enable --now "paragateway-backend@$release.service" "paragateway-gateway@$release.service"
echo "$release"

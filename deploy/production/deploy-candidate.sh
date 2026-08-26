#!/usr/bin/env bash
set -euo pipefail
commit=${1:?usage: deploy-candidate.sh <commit>}
production_commit=${PRODUCTION_COMMIT:?set PRODUCTION_COMMIT to the current production backend commit}
source_dir=${SOURCE_DIR:-/opt/paragateway/source}
release=${RELEASE_NAME:-candidate-${commit:0:12}-$(date -u +%Y%m%dT%H%M%SZ)}
release_dir=/opt/paragateway/releases/$release
candidate_env=${CANDIDATE_ENV_FILE:?set CANDIDATE_ENV_FILE}
production_env=${PRODUCTION_ENV_FILE:-/etc/paragateway/production.env}
production_config=${PRODUCTION_CONFIG_FILE:-/etc/paragateway/production-config.yaml}
devexpress_packages_dir=${DEVEXPRESS_PACKAGES_DIR:-/opt/paragateway/build/devexpress-packages}
devexpress_license_file=${DEVEXPRESS_LICENSE_FILE:-/etc/paragateway/build/devexpress-license}
expected_candidate_redis_db=${CANDIDATE_REDIS_DB_EXPECTED:-15}
allow_candidate_migrations=${ALLOW_CANDIDATE_MIGRATIONS:-0}
[[ "$commit" =~ ^[0-9a-f]{40}$ ]] || { echo 'commit must be a full lowercase SHA-1' >&2; exit 1; }
[[ "$production_commit" =~ ^[0-9a-f]{40}$ ]] || { echo 'PRODUCTION_COMMIT must be a full lowercase SHA-1' >&2; exit 1; }
[[ "$release" =~ ^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$ ]] || { echo 'invalid release name' >&2; exit 1; }
[[ "$allow_candidate_migrations" =~ ^[01]$ ]] || { echo 'ALLOW_CANDIDATE_MIGRATIONS must be 0 or 1' >&2; exit 1; }
command -v flock >/dev/null || { echo 'flock is required' >&2; exit 1; }
exec 9>/run/lock/paragateway-release.lock
flock -n 9 || { echo 'another ParaGateway release operation is running' >&2; exit 1; }
test -f "$candidate_env" && test -f "$production_env" && test -f "$production_config"
test -d "$devexpress_packages_dir" || { echo 'DevExpress offline package source is missing' >&2; exit 1; }
test -s "$devexpress_license_file" || { echo 'DevExpress build license is missing' >&2; exit 1; }
command -v dotnet >/dev/null || { echo '.NET SDK is required for the frontend build' >&2; exit 1; }
dotnet --list-sdks | grep -q '^10\.' || { echo '.NET 10 SDK is required for the frontend build' >&2; exit 1; }
command -v ss >/dev/null
for port in 8282 8284; do
  if ss -H -ltn "sport = :$port" | grep -q .; then
    echo "candidate port $port is already in use; stop the exact previous candidate units first" >&2
    exit 1
  fi
done
[ ! -e "$release_dir" ] && [ ! -e "/etc/paragateway/$release" ] || { echo 'release name already exists' >&2; exit 1; }
[ ! -e "/etc/systemd/system/paragateway-backend@$release.service.d" ] \
  && [ ! -e "/etc/systemd/system/paragateway-gateway@$release.service.d" ] \
  || { echo 'release has stale systemd drop-ins' >&2; exit 1; }
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
production_env_real=$(readlink -f -- "$production_env")
active_production_env_real=$(readlink -f -- "$active_production_env")
[ "$production_env_real" = "$active_production_env_real" ] || { echo 'PRODUCTION_ENV_FILE is not used by the active production backend' >&2; exit 1; }
production_config_real=$(readlink -f -- "$production_config")
active_production_config_real=$(readlink -f -- "$active_production_config")
[ "$production_config_real" = "$active_production_config_real" ] || { echo 'PRODUCTION_CONFIG_FILE is not used by the active production backend' >&2; exit 1; }
mkdir -p "$source_dir" /opt/paragateway/releases
if [ ! -d "$source_dir/.git" ]; then git clone https://github.com/fengtc/ParaGateway.git "$source_dir"; fi
git -C "$source_dir" fetch --prune origin
git -C "$source_dir" checkout --detach "$commit"
resolved_commit=$(git -C "$source_dir" rev-parse HEAD)
[ "$resolved_commit" = "$commit" ] || { echo 'checked out commit does not match the requested commit' >&2; exit 1; }
git -C "$source_dir" merge-base --is-ancestor "$commit" origin/main || { echo 'commit is not published on origin/main' >&2; exit 1; }
resolved_production_commit=$(git -C "$source_dir" rev-parse "$production_commit^{commit}")
[ "$resolved_production_commit" = "$production_commit" ] || { echo 'PRODUCTION_COMMIT does not resolve exactly' >&2; exit 1; }
candidate_migrations_only=0
if ! git -C "$source_dir" diff --quiet "$production_commit" "$commit" -- backend/migrations; then
  [ "$allow_candidate_migrations" = 1 ] || { echo 'backend migrations differ from production; set ALLOW_CANDIDATE_MIGRATIONS=1 only for an isolated disposable candidate database' >&2; exit 1; }
  candidate_migrations_only=1
fi
[ -z "$(git -C "$source_dir" status --porcelain --untracked-files=all)" ] || { echo 'server source worktree is not clean' >&2; exit 1; }
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
frontend_build_dir=$(mktemp -d)
nuget_config=$(mktemp)
frontend_log=$(mktemp)
trap 'rm -f "$tmp" "$dropin_tmp" "$data_env_tmp" "$nuget_config" "$frontend_log"; rm -rf "$frontend_build_dir"' EXIT
(cd "$source_dir/backend" && GOOS=linux GOARCH=amd64 CGO_ENABLED=0 go build -buildvcs=false -trimpath -ldflags='-s -w' -o "$tmp" ./cmd/server)
cat > "$nuget_config" <<EOF
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="DevExpress Offline" value="$devexpress_packages_dir" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
  </packageSources>
  <packageSourceMapping>
    <packageSource key="DevExpress Offline"><package pattern="DevExpress.*" /></packageSource>
    <packageSource key="nuget.org"><package pattern="*" /></packageSource>
  </packageSourceMapping>
</configuration>
EOF
frontend_project="$source_dir/frontend-blazor/ParaGateway.Frontend.csproj"
MSBUILDDISABLENODEREUSE=1 DOTNET_CLI_USE_MSBUILD_SERVER=0 \
  dotnet restore "$frontend_project" --configfile "$nuget_config" --disable-build-servers --nologo
DevExpress_License="$(cat "$devexpress_license_file")" MSBUILDDISABLENODEREUSE=1 DOTNET_CLI_USE_MSBUILD_SERVER=0 \
  dotnet publish "$frontend_project" -c Release -o "$frontend_build_dir" --no-restore --disable-build-servers \
    -p:UseAppHost=false -p:UseSharedCompilation=false --nologo 2>&1 | tee "$frontend_log"
if grep -Eq 'DX1000|DX1001|DX1002|DX1003|For evaluation purposes only' "$frontend_log"; then
  echo 'DevExpress license was not accepted by the frontend build' >&2
  exit 1
fi
test -s "$frontend_build_dir/wwwroot/index.html"
mapfile -t app_wasm < <(find "$frontend_build_dir/wwwroot/_framework" -maxdepth 1 -type f -name 'ParaGateway.Frontend*.wasm')
[ "${#app_wasm[@]}" -eq 1 ] || { echo 'could not uniquely identify the frontend application WASM' >&2; exit 1; }
[ "$(grep -aoc 'LCPv1!' "${app_wasm[0]}")" -eq 1 ] || { echo 'frontend WASM does not contain the DevExpress license marker' >&2; exit 1; }
printf '%s' "$commit" > "$frontend_build_dir/wwwroot/release-commit.txt"
install -d -o sub2api -g sub2api -m 750 "$release_dir/backend" "$release_dir/frontend"
install -o sub2api -g sub2api -m 750 "$tmp" "$release_dir/backend/paragateway-backend"
cp -a "$frontend_build_dir/." "$release_dir/frontend/"
chown -R sub2api:sub2api "$release_dir/frontend"
test -s "$release_dir/frontend/wwwroot/index.html"
release_commit_file="$release_dir/frontend/wwwroot/release-commit.txt"
test -s "$release_commit_file"
[ "$(tr -d '\r\n' < "$release_commit_file")" = "$commit" ] || { echo 'frontend archive commit does not match the requested commit' >&2; exit 1; }
install -d -o root -g sub2api -m 750 "/etc/paragateway/$release"
install -o root -g root -m 600 "$candidate_env" "/etc/paragateway/$release/candidate.env"
install -o root -g root -m 600 "$production_env_real" "/etc/paragateway/$release/production.env"
install -o root -g sub2api -m 640 "$production_config_real" "/etc/paragateway/$release/config.yaml"
printf '%s\n' "$production_env_real" > "/etc/paragateway/$release/production-env-source"
printf '%s\n' "$production_config_real" > "/etc/paragateway/$release/production-config-source"
printf '%s\n' "$production_commit" > "/etc/paragateway/$release/production-commit"
printf '%s\n' "$commit" > "/etc/paragateway/$release/target-commit"
if [ "$candidate_migrations_only" = 1 ]; then
  printf '%s -> %s\n' "$production_commit" "$commit" > "/etc/paragateway/$release/candidate-migrations-only"
  chmod 600 "/etc/paragateway/$release/candidate-migrations-only"
fi
cp "$(dirname "$0")/production.Candidate.Caddyfile.example" "/etc/paragateway/$release/Caddyfile"
cp "$(dirname "$0")/production.Caddyfile.example" "/etc/paragateway/$release/Caddyfile.production"
sed -i "s#%RELEASE_ROOT%#$release_dir/frontend/wwwroot#g" "/etc/paragateway/$release/Caddyfile" "/etc/paragateway/$release/Caddyfile.production"
sed "s#%RELEASE%#$release#g" "$(dirname "$0")/paragateway-backend.production.service.example" > "/etc/paragateway/$release/paragateway-backend.service"
sed "s#%RELEASE%#$release#g" "$(dirname "$0")/paragateway-gateway.production.service.example" > "/etc/paragateway/$release/paragateway-gateway.service"
sudo -u sub2api /usr/bin/caddy validate --config "/etc/paragateway/$release/Caddyfile" --adapter caddyfile
sudo -u sub2api /usr/bin/caddy validate --config "/etc/paragateway/$release/Caddyfile.production" --adapter caddyfile
install -o root -g root -m 644 "$(dirname "$0")/paragateway-backend@.service" /etc/systemd/system/
install -o root -g root -m 644 "$(dirname "$0")/paragateway-gateway@.service" /etc/systemd/system/
dropin_dir="/etc/systemd/system/paragateway-backend@$release.service.d"
install -d -o root -g root -m 755 "$dropin_dir"
printf 'DATA_DIR=/var/lib/paragateway-backend-%s\n' "$release" > "$data_env_tmp"
install -o root -g root -m 600 "$data_env_tmp" "/etc/paragateway/$release/data.env"
printf '[Service]\nEnvironmentFile=/etc/paragateway/%s/data.env\nReadWritePaths=\nReadWritePaths=/var/lib/paragateway-backend-%s\n' "$release" "$release" > "$dropin_tmp"
install -o root -g root -m 644 "$dropin_tmp" "$dropin_dir/data.conf"
systemctl daemon-reload
systemctl start "paragateway-backend@$release.service" "paragateway-gateway@$release.service"
echo "$release"

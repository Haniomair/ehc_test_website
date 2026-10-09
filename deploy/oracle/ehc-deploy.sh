#!/usr/bin/env bash
# Applies an upload on the server. Uploaded and run by deploy-oracle.ps1:
#
#   sudo bash ehc-deploy.sh code <site.tar.gz>        replace the code; the server's data is kept
#   sudo bash ehc-deploy.sh data <data.tar.gz>        REPLACE umbraco/Data (the database)
#   sudo bash ehc-deploy.sh media <media.tar.gz> [replace]   add/update media files (replace: clear the folder first)
#   sudo bash ehc-deploy.sh settings <file.json>      install appsettings.Staging.local.json
#   sudo bash ehc-deploy.sh rollback                  put the previous code back
set -euo pipefail

APP=/var/www/ehc
RELEASES=/var/lib/ehc/releases
MODE="${1:?mode}"
FILE="${2:-}"

[ "$(id -u)" -eq 0 ] || { echo 'Run as root (sudo).' >&2; exit 1; }

started() {
  chown -R ehc:ehc "$APP"
  systemctl start ehc
  # the first request starts Umbraco; make it here rather than for the first visitor
  if curl -fsS -o /dev/null --max-time 120 --retry 30 --retry-delay 2 --retry-connrefused http://127.0.0.1:5000/; then
    echo 'Site is up.'
  else
    echo 'The site did not answer. Last log lines:' >&2
    journalctl -u ehc -n 40 --no-pager >&2
    exit 1
  fi
}

install_code() {
  systemctl stop ehc
  # remove the old code, keep the server's own data and settings
  find "$APP" -mindepth 1 -maxdepth 1 ! -name umbraco ! -name wwwroot ! -name 'appsettings.*.local.json' -exec rm -rf {} +
  [ -d "$APP/umbraco" ] && find "$APP/umbraco" -mindepth 1 -maxdepth 1 ! -name Data ! -name Logs ! -name mediacache -exec rm -rf {} +
  [ -d "$APP/wwwroot" ] && find "$APP/wwwroot" -mindepth 1 -maxdepth 1 ! -name media ! -name tiles -exec rm -rf {} +
  # map tiles are replaced only when the upload has them
  if tar -tzf "$1" | grep -qE '^(\./)?wwwroot/tiles/.'; then rm -rf "$APP/wwwroot/tiles"; fi
  tar -xzf "$1" -C "$APP" --no-same-owner \
    --exclude='umbraco/Data' --exclude='umbraco/Logs' --exclude='umbraco/mediacache' \
    --exclude='wwwroot/media' --exclude='appsettings.*.local.json'
  started
}

case "$MODE" in
  code)
    [ -f "$FILE" ] || { echo "No file: $FILE" >&2; exit 1; }
    mkdir -p "$RELEASES"
    [ -f "$RELEASES/current.tar.gz" ] && mv -f "$RELEASES/current.tar.gz" "$RELEASES/previous.tar.gz"
    mv -f "$FILE" "$RELEASES/current.tar.gz"
    install_code "$RELEASES/current.tar.gz"
    ;;
  rollback)
    [ -f "$RELEASES/previous.tar.gz" ] || { echo 'No previous release on this server.' >&2; exit 1; }
    mv -f "$RELEASES/current.tar.gz" "$RELEASES/failed.tar.gz"
    mv -f "$RELEASES/previous.tar.gz" "$RELEASES/current.tar.gz"
    install_code "$RELEASES/current.tar.gz"
    ;;
  data)
    [ -f "$FILE" ] || { echo "No file: $FILE" >&2; exit 1; }
    systemctl stop ehc
    rm -rf "$APP/umbraco/Data"
    mkdir -p "$APP/umbraco/Data"
    tar -xzf "$FILE" -C "$APP/umbraco/Data" --no-same-owner
    # caches are rebuilt by the site
    rm -rf "$APP/umbraco/Data/TEMP"
    rm -f "$FILE"
    started
    ;;
  media)
    [ -f "$FILE" ] || { echo "No file: $FILE" >&2; exit 1; }
    if [ "${3:-}" = replace ]; then rm -rf "$APP/wwwroot/media"; fi
    mkdir -p "$APP/wwwroot/media"
    tar -xzf "$FILE" -C "$APP/wwwroot/media" --no-same-owner
    chown -R ehc:ehc "$APP/wwwroot/media"
    rm -f "$FILE"
    echo 'Media updated.'
    ;;
  settings)
    [ -f "$FILE" ] || { echo "No file: $FILE" >&2; exit 1; }
    install -o ehc -g ehc -m 640 "$FILE" "$APP/appsettings.Staging.local.json"
    rm -f "$FILE"
    if [ -f "$APP/EHC.Web.dll" ]; then systemctl stop ehc; started; else echo 'Settings installed.'; fi
    ;;
  *)
    echo "Unknown mode: $MODE" >&2; exit 1 ;;
esac

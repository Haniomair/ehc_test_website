#!/usr/bin/env bash
# One-time preparation of an Ubuntu server (arm64 or x64) for the EHC website: .NET runtime, Caddy (HTTPS, HTTP/2
# and HTTP/3 with automatic certificates), a service account and a systemd service.
# Safe to run again; a re-run also updates the runtime and Caddy.
#
#   sudo bash server-setup.sh <domain>          e.g. sudo bash server-setup.sh ehctest.site
#
# deploy-oracle.ps1 -Setup uploads and runs this file.
set -euo pipefail

DOMAIN="${1:?usage: server-setup.sh <domain>}"
APP=/var/www/ehc
HOME_DIR=/var/lib/ehc
PORT=5000

[ "$(id -u)" -eq 0 ] || { echo 'Run as root (sudo).' >&2; exit 1; }
export DEBIAN_FRONTEND=noninteractive

echo '== packages'
apt-get update -q
apt-get install -y -q curl ca-certificates gnupg debian-keyring debian-archive-keyring apt-transport-https \
  iptables-persistent unattended-upgrades

echo '== ASP.NET Core 10 runtime'
if ! apt-get install -y -q aspnetcore-runtime-10.0 2>/dev/null; then
  # not in this release's archive: Microsoft's install script (same runtime, updated by re-running this setup)
  curl -fsSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
  bash /tmp/dotnet-install.sh --channel 10.0 --runtime aspnetcore --install-dir /usr/lib/dotnet
  ln -sf /usr/lib/dotnet/dotnet /usr/bin/dotnet
fi
dotnet --list-runtimes | grep -q 'Microsoft.AspNetCore.App 10\.' || { echo 'ASP.NET Core 10 runtime not found.' >&2; exit 1; }

echo '== Caddy'
if [ ! -f /usr/share/keyrings/caddy-stable-archive-keyring.gpg ]; then
  curl -1sLf https://dl.cloudsmith.io/public/caddy/stable/gpg.key | gpg --dearmor -o /usr/share/keyrings/caddy-stable-archive-keyring.gpg
  curl -1sLf https://dl.cloudsmith.io/public/caddy/stable/debian.deb.txt > /etc/apt/sources.list.d/caddy-stable.list
  chmod o+r /usr/share/keyrings/caddy-stable-archive-keyring.gpg /etc/apt/sources.list.d/caddy-stable.list
  apt-get update -q
fi
apt-get install -y -q caddy

echo '== service account and folders'
id ehc >/dev/null 2>&1 || useradd --system --home-dir "$HOME_DIR" --create-home --shell /usr/sbin/nologin ehc
mkdir -p "$APP/umbraco/Data" "$APP/umbraco/Logs" "$APP/wwwroot/media" "$HOME_DIR/releases"
chown -R ehc:ehc "$APP" "$HOME_DIR"
chmod 750 "$HOME_DIR"

echo '== systemd service'
cat > /etc/systemd/system/ehc.service <<EOF
[Unit]
Description=EHC website
After=network-online.target
Wants=network-online.target

[Service]
User=ehc
Group=ehc
WorkingDirectory=$APP
ExecStart=/usr/bin/dotnet $APP/EHC.Web.dll
Restart=always
RestartSec=5
KillSignal=SIGINT
TimeoutStopSec=30
SyslogIdentifier=ehc
Environment=ASPNETCORE_ENVIRONMENT=Staging
Environment=ASPNETCORE_URLS=http://127.0.0.1:$PORT
# Caddy terminates HTTPS: trust its X-Forwarded-For / X-Forwarded-Proto so the site sees https and the visitor's IP
Environment=ASPNETCORE_FORWARDEDHEADERS_ENABLED=true
Environment=DOTNET_NOLOGO=1
# data-protection keys (backoffice sign-in) are kept under the account's home, so restarts don't sign editors out
Environment=HOME=$HOME_DIR
NoNewPrivileges=true
PrivateTmp=true
ProtectSystem=full
ReadWritePaths=$APP $HOME_DIR

[Install]
WantedBy=multi-user.target
EOF
systemctl daemon-reload
systemctl enable ehc >/dev/null

echo '== Caddy site'
cat > /etc/caddy/Caddyfile <<EOF
$DOMAIN {
	encode zstd gzip
	reverse_proxy 127.0.0.1:$PORT
}
EOF
systemctl enable caddy >/dev/null
systemctl reload caddy 2>/dev/null || systemctl restart caddy

echo '== firewall'
# Oracle's Ubuntu images reject everything but SSH in iptables, in addition to the cloud security list
for rule in 'tcp 80' 'tcp 443' 'udp 443'; do
  set -- $rule
  iptables -C INPUT -p "$1" --dport "$2" -m conntrack --ctstate NEW -j ACCEPT 2>/dev/null \
    || iptables -I INPUT 1 -p "$1" --dport "$2" -m conntrack --ctstate NEW -j ACCEPT
done
netfilter-persistent save >/dev/null

echo '== automatic security updates'
dpkg-reconfigure -f noninteractive unattended-upgrades >/dev/null

echo
echo "Done. Next: deploy-oracle.ps1 -Settings, then deploy-oracle.ps1 -Code -SeedData for the first deploy."
[ -f "$APP/EHC.Web.dll" ] && systemctl restart ehc || true

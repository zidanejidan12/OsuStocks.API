#!/usr/bin/env bash
# server-bootstrap.sh — prepare a fresh Hetzner CX33 (Ubuntu 22.04/24.04) to host OsuStocks.
# Run as root on the brand-new box:   bash server-bootstrap.sh
#
# It is idempotent-ish: safe to re-run. It does NOT lock you out — SSH hardening
# (disabling root/password login) is a clearly-marked OPTIONAL last step you run
# only AFTER confirming the new user can log in.
set -euo pipefail

DEPLOY_USER="${DEPLOY_USER:-deploy}"
SSH_PUBKEY="${SSH_PUBKEY:-}"   # paste your public key:  SSH_PUBKEY="ssh-ed25519 AAAA... you@host" bash server-bootstrap.sh

echo "==> Updating packages"
export DEBIAN_FRONTEND=noninteractive
apt-get update -y && apt-get upgrade -y
apt-get install -y ca-certificates curl gnupg ufw fail2ban git

echo "==> Creating deploy user '${DEPLOY_USER}'"
if ! id "${DEPLOY_USER}" &>/dev/null; then
    adduser --disabled-password --gecos "" "${DEPLOY_USER}"
fi
usermod -aG sudo "${DEPLOY_USER}"

if [[ -n "${SSH_PUBKEY}" ]]; then
    install -d -m 700 -o "${DEPLOY_USER}" -g "${DEPLOY_USER}" "/home/${DEPLOY_USER}/.ssh"
    echo "${SSH_PUBKEY}" > "/home/${DEPLOY_USER}/.ssh/authorized_keys"
    chmod 600 "/home/${DEPLOY_USER}/.ssh/authorized_keys"
    chown "${DEPLOY_USER}:${DEPLOY_USER}" "/home/${DEPLOY_USER}/.ssh/authorized_keys"
    echo "   added SSH key for ${DEPLOY_USER}"
else
    echo "   WARNING: no SSH_PUBKEY given — add /home/${DEPLOY_USER}/.ssh/authorized_keys before hardening SSH."
fi

echo "==> Installing Docker Engine + Compose plugin"
if ! command -v docker &>/dev/null; then
    install -m 0755 -d /etc/apt/keyrings
    curl -fsSL https://download.docker.com/linux/ubuntu/gpg | gpg --dearmor -o /etc/apt/keyrings/docker.gpg
    chmod a+r /etc/apt/keyrings/docker.gpg
    echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.gpg] \
https://download.docker.com/linux/ubuntu $(. /etc/os-release && echo "$VERSION_CODENAME") stable" \
        > /etc/apt/sources.list.d/docker.list
    apt-get update -y
    apt-get install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin
fi
usermod -aG docker "${DEPLOY_USER}"
systemctl enable --now docker

echo "==> Firewall (ufw): allow SSH/HTTP/HTTPS only"
ufw allow OpenSSH
ufw allow 80/tcp
ufw allow 443/tcp
ufw --force enable

echo "==> fail2ban (default sshd jail)"
systemctl enable --now fail2ban

echo "==> Swap (2G) — CX33 has 8G RAM; a little swap protects against OOM during builds"
if [[ ! -f /swapfile ]]; then
    fallocate -l 2G /swapfile && chmod 600 /swapfile && mkswap /swapfile && swapon /swapfile
    echo '/swapfile none swap sw 0 0' >> /etc/fstab
fi

cat <<EOF

==================================================================
Base setup done. Next:
  1. From your laptop, TEST:   ssh ${DEPLOY_USER}@<server-ip>
     (must succeed with your key before you harden SSH below.)
  2. Clone the repo as ${DEPLOY_USER} and deploy (see deploy/README.md).

OPTIONAL SSH hardening — ONLY after step 1 works, run as root:
  sed -i 's/^#\?PermitRootLogin.*/PermitRootLogin no/' /etc/ssh/sshd_config
  sed -i 's/^#\?PasswordAuthentication.*/PasswordAuthentication no/' /etc/ssh/sshd_config
  systemctl restart ssh
==================================================================
EOF

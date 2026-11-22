#!/usr/bin/env bash
# =============================================================================
# SshSecMonitorer – SFTP-Benutzer für den Monitorer auf einer VM anlegen
# Für Raspberry Pi OS (Raspbian) >= 11 "bullseye" (32 und 64 Bit). Als root ausführen:  sudo bash setup-sftp-user-raspios.sh
#
# Der Benutzer darf danach NUR:
#   - sich per SSH-Schlüssel anmelden (kein Passwort, keine Shell, kein TTY, keine Weiterleitungen)
#   - per SFTP lesend auf den Agent-Ordner zugreifen (chroot, internal-sftp -R)
# Das Skript ist wiederholbar: erneutes Ausführen aktualisiert Schlüssel und Einstellungen.
# =============================================================================
set -euo pipefail

# ----------------------------------------------------------------------------- Platzhalter ausfüllen
# Ordner, in den der Agent seine monitoring.json schreibt (AGENT_DATA_DIR des Agents), z. B. /opt/sshsecmonitorer/agent
MONITORING_DIR="<PFAD_ZUM_AGENT_DATENORDNER>"
# Öffentlicher Schlüssel des Monitorers (eine Zeile, z. B. "ssh-ed25519 AAAA... monitorer").
# Aus dem privaten Schlüssel erzeugen:  ssh-keygen -y -f monitorer_key
PUBLIC_KEY="<OEFFENTLICHER_SCHLUESSEL_DES_MONITORERS>"
# IP-Adresse(n) des Monitorers, kommagetrennt (z. B. 10.10.10.5). Leer lassen = Anmeldung von überall erlaubt.
MONITORER_IP="<IP_DES_MONITORERS>"
# Name des Benutzers auf der VM
USER_NAME="sshsecmonitorer"

# ----------------------------------------------------------------------------- Prüfungen
fail() { echo "FEHLER: $*" >&2; exit 1; }
info() { echo "==> $*"; }

[[ $EUID -eq 0 ]] || fail "Bitte als root ausführen (sudo bash $0)."

for value in "$MONITORING_DIR" "$PUBLIC_KEY" "$MONITORER_IP"; do
  [[ $value != *"<"*">"* ]] || fail "Bitte zuerst alle Platzhalter <...> oben im Skript ausfüllen (MONITORER_IP darf leer sein)."
done

if [[ -r /etc/os-release ]]; then
  # shellcheck disable=SC1091
  . /etc/os-release
  # 32 Bit meldet ID=raspbian, 64 Bit ID=debian (erkennbar an /etc/rpi-issue)
  if [[ ${ID:-} != "raspbian" && ! -e /etc/rpi-issue ]]; then
    echo "WARNUNG: Skript ist für Raspberry Pi OS gedacht (gefunden: ${ID:-unbekannt})." >&2
  elif dpkg --compare-versions "${VERSION_ID:-0}" lt "11"; then
    fail "Raspberry Pi OS ${VERSION_ID} wird nicht unterstützt (mindestens 11 \"bullseye\")."
  fi
fi

command -v sshd >/dev/null || fail "OpenSSH-Server ist nicht installiert (apt install openssh-server)."
[[ $USER_NAME =~ ^[a-z_][a-z0-9_-]{0,31}$ ]] || fail "Ungültiger Benutzername: $USER_NAME"
[[ $PUBLIC_KEY =~ ^(ssh-ed25519|ssh-rsa|ecdsa-sha2-nistp(256|384|521))\ [A-Za-z0-9+/=]+(\ .*)?$ ]] \
  || fail "PUBLIC_KEY sieht nicht wie ein öffentlicher OpenSSH-Schlüssel aus."
if [[ -n $MONITORER_IP ]]; then
  [[ $MONITORER_IP =~ ^[0-9A-Fa-f.:/,*]+$ ]] || fail "MONITORER_IP enthält ungültige Zeichen."
fi

MONITORING_DIR="${MONITORING_DIR%/}"
[[ $MONITORING_DIR == /* ]] || fail "MONITORING_DIR muss ein absoluter Pfad sein."
CHROOT_DIR="$(dirname "$MONITORING_DIR")"
[[ $CHROOT_DIR != "/" ]] || fail "MONITORING_DIR darf nicht direkt unter / liegen (es wird ein eigener Chroot-Ordner gebraucht, z. B. /opt/sshsecmonitorer/agent)."
SUBDIR="$(basename "$MONITORING_DIR")"

NOLOGIN="$(command -v nologin || echo /usr/sbin/nologin)"
# OpenSSH < 8.7 kennt "KbdInteractiveAuthentication" in sshd_config noch nicht (dort: ChallengeResponseAuthentication)
SSH_VERSION="$(ssh -V 2>&1 | sed -n 's/^OpenSSH_\([0-9]*\.[0-9]*\).*/\1/p')"
KBD_OPTION="KbdInteractiveAuthentication"
if [[ -n $SSH_VERSION ]] && awk -v v="$SSH_VERSION" 'BEGIN { exit !(v < 8.7) }'; then
  KBD_OPTION="ChallengeResponseAuthentication"
fi

# ----------------------------------------------------------------------------- Benutzer
if id "$USER_NAME" >/dev/null 2>&1; then
  info "Benutzer $USER_NAME existiert bereits – wird aktualisiert."
else
  info "Lege Systembenutzer $USER_NAME an."
  useradd --system --create-home --home-dir "/home/$USER_NAME" --shell "$NOLOGIN" \
    --comment "SshSecMonitorer SFTP (read-only)" "$USER_NAME"
fi
passwd --lock "$USER_NAME" >/dev/null          # kein Passwort-Login
usermod --shell "$NOLOGIN" "$USER_NAME" 2>/dev/null || true

# ----------------------------------------------------------------------------- Schlüssel (außerhalb des Chroots)
HOME_DIR="$(getent passwd "$USER_NAME" | cut -d: -f6)"
install -d -m 700 -o "$USER_NAME" -g "$USER_NAME" "$HOME_DIR/.ssh"
OPTIONS="restrict"
[[ -n $MONITORER_IP ]] && OPTIONS="restrict,from=\"$MONITORER_IP\""
printf '%s %s\n' "$OPTIONS" "$PUBLIC_KEY" > "$HOME_DIR/.ssh/authorized_keys"
chown "$USER_NAME:$USER_NAME" "$HOME_DIR/.ssh/authorized_keys"
chmod 600 "$HOME_DIR/.ssh/authorized_keys"
info "Öffentlicher Schlüssel hinterlegt (${OPTIONS})."

# ----------------------------------------------------------------------------- Chroot-Ordner
# sshd verlangt: jeder Ordner bis zum Chroot gehört root und ist für niemanden sonst beschreibbar.
mkdir -p "$MONITORING_DIR"
path="$CHROOT_DIR"
while [[ $path != "/" ]]; do
  owner="$(stat -c %u "$path")"
  mode="$(stat -c %a "$path")"
  if [[ $owner != 0 ]] || (( (8#$mode & 8#022) != 0 )); then
    if [[ $path == "$CHROOT_DIR" ]]; then
      info "Setze $path auf root:root 755 (Voraussetzung für ChrootDirectory)."
      chown root:root "$path"
      chmod 755 "$path"
    else
      fail "$path gehört nicht root oder ist für Gruppe/Andere beschreibbar – als Chroot ungeeignet. Bitte einen anderen MONITORING_DIR wählen."
    fi
  fi
  path="$(dirname "$path")"
done
# der Agent (UID 10001) schreibt in den Datenordner, alle anderen dürfen nur lesen
chmod 755 "$MONITORING_DIR"
[[ -f "$MONITORING_DIR/monitoring.json" ]] && chmod o+r "$MONITORING_DIR/monitoring.json"

# ----------------------------------------------------------------------------- sshd-Konfiguration
# Der Match-Block steht am ENDE der sshd_config (nicht in sshd_config.d/, das am Anfang eingebunden wird –
# ein Match-Block dort würde die folgenden globalen Einstellungen mit einschließen).
SSHD_CONFIG="/etc/ssh/sshd_config"
BEGIN_MARK="# >>> SshSecMonitorer $USER_NAME >>>"
END_MARK="# <<< SshSecMonitorer $USER_NAME <<<"
BACKUP="$SSHD_CONFIG.bak-sshsecmonitorer-$(date +%Y%m%d%H%M%S)"
cp -a "$SSHD_CONFIG" "$BACKUP"
sed -i "/^${BEGIN_MARK//\//\\/}\$/,/^${END_MARK//\//\\/}\$/d" "$SSHD_CONFIG"
cat >> "$SSHD_CONFIG" <<CONF
$BEGIN_MARK
# nur lesendes SFTP im Agent-Ordner, sonst nichts
Match User $USER_NAME
    AuthenticationMethods publickey
    PasswordAuthentication no
    $KBD_OPTION no
    ForceCommand internal-sftp -R -d /$SUBDIR
    ChrootDirectory $CHROOT_DIR
    PermitTTY no
    AllowTcpForwarding no
    AllowStreamLocalForwarding no
    AllowAgentForwarding no
    X11Forwarding no
    PermitTunnel no
    GatewayPorts no
$END_MARK
CONF

if grep -Eiq '^\s*Allow(Users|Groups)\s' "$SSHD_CONFIG" /etc/ssh/sshd_config.d/*.conf 2>/dev/null; then
  echo "WARNUNG: AllowUsers/AllowGroups ist gesetzt – $USER_NAME dort ergänzen, sonst wird die Anmeldung abgelehnt." >&2
fi

info "Prüfe sshd-Konfiguration."
if ! sshd -t; then
  cp -a "$BACKUP" "$SSHD_CONFIG"
  fail "sshd -t meldet einen Fehler – alte Konfiguration wiederhergestellt ($BACKUP)."
fi
# SSH ist auf Raspberry Pi OS standardmäßig deaktiviert
if ! systemctl is-active --quiet ssh; then
  info "SSH-Dienst läuft nicht – wird aktiviert und gestartet."
  systemctl enable --now ssh
fi
systemctl reload ssh 2>/dev/null || systemctl restart ssh
info "sshd neu geladen."

# ----------------------------------------------------------------------------- Ergebnis
FINGERPRINT="$(ssh-keygen -lf /etc/ssh/ssh_host_ed25519_key.pub 2>/dev/null | awk '{print $2}' || true)"
cat <<DONE

Fertig. Im Monitorer eintragen:
  SSH-Benutzer : $USER_NAME
  Pfad         : /$SUBDIR/monitoring.json   (relativ zum Chroot $CHROOT_DIR)
  Host-Key     : ${FINGERPRINT:-(ssh-keygen -lf /etc/ssh/ssh_host_ed25519_key.pub)}
Danach unter "SSH-Zugänge" -> "Verbindung testen" prüfen.
DONE

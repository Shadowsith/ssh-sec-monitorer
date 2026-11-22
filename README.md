# SshSecMonitorer

Individuelles, sicheres Monitoring über SSH (SFTP) – bestehend aus drei Teilen:

| Teil | Technik | Aufgabe |
|---|---|---|
| **Monitorer** (`Monitorer/`) | ASP.NET 10, Razor Pages, REST-Controller, PostgreSQL, Dapper + FluentMigrator | Holt zyklisch per SFTP (über SSH) die `monitoring.json` der Agents, wertet sie aus, zeigt Übersicht + Details, Live-Checks (HTTP(S), WS(S), MQTT(S)), Warnmails |
| **Agent** (`Agent/`) | Python 3.13, Docker | Läuft auf jeder Ubuntu/Debian-VM, liest Host- und Container-Daten **nur lesend** und schreibt ausschließlich seine `monitoring.json` |
| **Konfigurator** (`Configurator/`) | Avalonia UI (.NET 10), SQLite, Dapper + FluentMigrator | Erstellt die `config.yml` der Agents (Copy & Paste / Datei), speichert Konfigurationen lokal |

```
 ┌──────────────┐  SFTP (read, SSH) ┌──────────────────────────── VM ────────────────────────────┐
 │  Monitorer   │ ────────────────▶ │  /opt/sshsecmonitorer/agent/monitoring.json                 │
 │  + Postgres  │                   │        ▲ schreibt (einziger Schreibzugriff)                │
 │              │ HTTP/WS/MQTT ───▶ │  Agent ──(internes Netz)──▶ docker-socket-proxy (POST=0)   │
 └──────────────┘  (Live-Checks)    │    └─ /proc, / read-only                                    │
                                    └─────────────────────────────────────────────────────────────┘
```

## Schnellstart Monitorer (Docker)

```bash
cd docker-compose
./setup.sh                    # erzeugt .env mit zufälligen Secrets, fragt Admin-User ab
docker compose up -d --build
```

- Standardmäßig nur auf `127.0.0.1:8080` gebunden → dahinter einen Reverse Proxy mit HTTPS (nginx, Traefik, Caddy) setzen.
  Für einen Test ohne HTTPS: `MONITORER_BIND=0.0.0.0` und `SECURE_COOKIE=false` in `.env`.
- Ohne LDAP muss ein Admin existieren: entweder über `ADMIN_USERNAME` / `ADMIN_PASSWORD` in `.env`
  (wird nur angelegt, solange noch kein Benutzer existiert) oder über die Seite **/Setup** beim ersten Aufruf.
- Das Volume `dpkeys` enthält den Data-Protection-Schlüsselring, mit dem SSH-Keys und Passwörter in der DB verschlüsselt sind – **mitsichern**, sonst sind gespeicherte Secrets unbrauchbar.

### Lokale Entwicklung

```bash
docker run -d --name ssm-dev-postgres -e POSTGRES_PASSWORD=postgres -p 5432:5432 postgres:17-alpine
cd Monitorer
dotnet run                    # http://localhost:5001, Swagger unter /swagger (Development)
```

Die Datenbank wird beim Start automatisch angelegt und migriert (FluentMigrator).

## Agent auf einer VM installieren

1. Ordner `Agent/` auf die VM kopieren, z. B. nach `/opt/sshsecmonitorer/agent-src`.
2. Datenverzeichnis anlegen – der Agent-Container läuft als UID 10001:
   ```bash
   sudo mkdir -p /opt/sshsecmonitorer/agent
   sudo chown 10001:10001 /opt/sshsecmonitorer/agent
   sudo chmod 755 /opt/sshsecmonitorer/agent
   ```
3. `config.yml` mit dem Konfigurator erzeugen (oder die Vorlage anpassen) und neben `docker-compose.yml` legen.
4. Starten: `docker compose up -d --build`
5. SSH-Benutzer für den Monitorer anlegen – am einfachsten mit dem passenden Skript aus `Monitorer/Assets/Scripts/`
   (im Monitorer unter **SSH-Zugänge → Bearbeiten → „Nutzer auf VM anlegen“**: System wählen, Platzhalter ausfüllen,
   „Übernehmen“, kopieren / herunterladen):

   | System | Skript |
   |---|---|
   | Ubuntu ≥ 22.04 | `setup-sftp-user-ubuntu.sh` |
   | Arch Linux / Manjaro | `setup-sftp-user-arch.sh` |
   | Raspberry Pi OS / Raspbian ≥ 11 | `setup-sftp-user-raspios.sh` |

   `sudo bash setup-sftp-user-<system>.sh` legt den Benutzer `sshsecmonitorer` an, der sich nur per Schlüssel (optional nur von
   der IP des Monitorers) anmelden und nur lesend per SFTP im Agent-Ordner (chroot) zugreifen darf, sichert und prüft die
   `sshd_config`, startet den SSH-Dienst falls nötig und gibt Pfad und Host-Key für den Monitorer aus. Die drei Skripte
   unterscheiden sich nur in der Systemprüfung, dem Paket-Hinweis und dem Namen des SSH-Dienstes (`ssh` / `sshd`) – bei
   Änderungen alle drei anpassen. Von Hand geht es so (kein sudo, kein Docker). Der Monitorer holt die Datei per **SFTP** – gleicher Port und
   gleiche Anmeldung wie SSH, in OpenSSH unter Ubuntu/Debian standardmäßig aktiv:
   ```bash
   sudo adduser --disabled-password --gecos "" ssm-reader
   sudo -u ssm-reader mkdir -p ~ssm-reader/.ssh
   # öffentlichen Schlüssel des Monitorers eintragen, optional eingeschränkt:
   # from="10.0.0.5",no-port-forwarding,no-agent-forwarding,no-X11-forwarding,no-pty ssh-ed25519 AAAA... monitorer
   sudo -u ssm-reader nano ~ssm-reader/.ssh/authorized_keys
   ```
   Empfohlen: den Benutzer auf reines, lesendes SFTP im Agent-Ordner beschränken – ans Ende von `/etc/ssh/sshd_config`:
   ```
   Match User ssm-reader
       ForceCommand internal-sftp -R
       ChrootDirectory /opt/sshsecmonitorer
       AllowTcpForwarding no
       X11Forwarding no
       PermitTTY no
   ```
   `/opt/sshsecmonitorer` muss `root` gehören und darf für niemanden sonst beschreibbar sein (`sudo chown root:root /opt/sshsecmonitorer && sudo chmod 755 /opt/sshsecmonitorer`).
   Prüfen und übernehmen: `sudo sshd -t && sudo systemctl reload ssh`.
6. Im Monitorer unter **SSH-Zugänge** den privaten Schlüssel hinterlegen und unter **Agents** die VM anlegen. Pfad:
   `/opt/sshsecmonitorer/agent/monitoring.json`, mit Chroot `/agent/monitoring.json`. Der SSH-Host-Key wird beim ersten Abruf
   gepinnt (TOFU) und danach strikt geprüft – für Produktion den Fingerprint vorab eintragen (`ssh-keyscan host | ssh-keygen -lf -`).

Test ohne Docker: `python -m ssm_agent --config config.yml --once` (mit `proc_path: /proc`, `rootfs_path: /`, `output_file: ./data/monitoring.json`).

### Lokaler Agent (Monitorer-Host selbst)

Für den Host, auf dem der Monitorer läuft, braucht es kein SSH: Der Agent läuft im selben Compose-Projekt und schreibt in
das Named Volume `local-agent-data`, das der Monitorer read-only unter `/app/agents/local` mountet.

```bash
cd docker-compose
docker compose --profile local-agent up -d --build
```

Danach unter **Agents → Agent hinzufügen** die Verbindung **Lokal** wählen und als Ordner `local` eintragen.
Konfiguration: `docker-compose/local-agent/config.yml` (gleiches Format wie `Agent/config.yml`).

- Gemountet wird immer der **Ordner**, nie die Datei – der Agent ersetzt `monitoring.json` atomar per Rename.
- Der Monitorer liest nur unterhalb von `Monitoring:LocalAgentRoot` (`/app/agents`), lehnt symbolische Links (Ordner und
  Datei) ab und begrenzt die Größe auf `Monitoring:MaxFileBytes`. Weitere lokale Agents: zusätzliches Volume nach
  `/app/agents/<ordner>:ro` mounten (Ordnername `a–z 0–9 - _`).
- Datei fehlt / nicht lesbar → Status *Unreachable* (VM-Alarm), veraltete `generated_at` → *AgentStale* – wie bei SFTP.

### Sicherheitsmodell des Agents

- keine offenen Ports, nur ein `internal: true`-Netz zum Socket-Proxy
- Docker-Socket nur über `tecnativa/docker-socket-proxy` mit `POST=0` (reine Lese-API: list/inspect/stats/logs)
- `/proc` und `/` read-only gemountet, Container `read_only`, `cap_drop: ALL`, `no-new-privileges`, nicht-root
- einziger Schreibzugriff: `/data/monitoring.json` (atomar per Temp-Datei + Rename)

## monitoring.json (Schema 1)

```jsonc
{
  "schema_version": 1,
  "generated_at": "2026-11-20T07:14:02Z",
  "agent":  { "name": "vm-web-01", "version": "0.1.0", "intervals": { "write": 15, "host": 10, "docker": 30 } },
  "host": {
    "current": { "hostname": "…", "uptime_seconds": 12345, "cpu_percent": 8.8, "cpu_count": 4, "load": [0.1, 0.2, 0.3],
                 "memory": { "total": 0, "used": 0, "available": 0, "percent": 4.7 }, "swap": { … },
                 "disks":   [ { "mount": "/", "total": 0, "used": 0, "free": 0, "percent": 28.9 } ],
                 "network": [ { "interface": "eth0", "rx_total": 0, "tx_total": 0, "rx_bps": 0.0, "tx_bps": 0.0 } ],
                 "collected_at": 1795158841 },
    "history": { "cpu_percent": [[1795158841, 8.8], …], "memory_percent": […], "disk_percent:/": […], "net_rx_bps": […], "net_tx_bps": […] }
  },
  "docker": {
    "available": true, "error": null, "engine": { "version": "27.3.1", "api_version": "1.47" },
    "containers": [ { "id": "…", "name": "web", "image": "nginx:1.27", "state": "running", "health": "healthy",
                      "exit_code": 0, "restart_count": 0, "started_at": "…", "cpu_percent": 0.4,
                      "memory": { "usage": 0, "limit": 0, "percent": 1.2 }, "network": { "rx_total": 0, "tx_total": 0 },
                      "block_io": { "read_bps": 1250000.0, "write_bps": 310000.0, "read_total": 0, "write_total": 0, "available": true },
                      "ports": [ { "host_ip": "0.0.0.0", "host_port": 8080, "container_port": 80, "protocol": "tcp", "localhost_only": false } ],
                      "logs": { "window_seconds": 300, "scanned_lines": 120, "error_count": 0, "warning_count": 2, "tail": ["…"] } } ],
    "history": { "web.cpu_percent": [[ts, v], …], "web.memory_percent": […] }
  }
}
```

`ports` enthält nur nach außen veröffentlichte Host-Ports (`-p` / `ports:`); nur intern exponierte Ports fehlen, nur an `127.0.0.1` gebundene nur mit `docker.ports.include_localhost: true`. Mit `docker.ports.enabled: false` fehlt der Schlüssel ganz und der Monitorer blendet die Ports aus.

`block_io` ist das Festplatten-I/O des Containers (Block-Devices laut cgroup): `read_bps` / `write_bps` = aktuelle Rate seit der letzten Messung (`docker.interval`), `read_total` / `write_total` = Summe seit Containerstart, nur mit `docker.block_io.totals: true`.

Was getrackt wird, wie oft und wie lange der Verlauf in der Datei bleibt, steuert die `config.yml` (`interval`, `history_retention`, …).

## Monitorer – Aufbau

```
Monitorer/
  Controllers/        REST (api/auth, api/agents, api/ssh-credentials, api/live-checks, api/users, api/setup, api/health)
  Pages/              Razor Pages: Login, Setup, Übersicht, Agents (Liste/Detail/Bearbeiten), SSH-Zugänge, Live-Checks, Benutzer
  Services/           Geschäftslogik (Auth, LDAP, SFTP-Abruf, Auswertung, Alerts, Live-Checks, …)
  Repository/         Dapper-SQL, eine Klasse pro Tabelle
  Data/Db|Dto|Monitoring|Result
  Migrations/         FluentMigrator
  BackgroundServices/ Polling der Agents, Live-Checks
  Middleware/ Filters/ Extensions/ Assets/
```

- **Lastverteilung:** Jeder Agent (und jeder Live-Check) bekommt einen festen Zeitversatz innerhalb seines Intervalls – bei 100 VMs mit 60 s Intervall starten also etwa 17 Abrufe je 10 s statt 100 auf einmal, auch direkt nach einem Neustart. Zusätzlich begrenzt: `Monitoring:MaxParallelPolls` (gleichzeitige SFTP-Abrufe, Standard 8) und `Monitoring:MaxPollsPerTick` (Starts je Tick, 0 = unbegrenzt; Rest folgt im nächsten Tick, am längsten wartende zuerst). Ein Agent wird nie doppelt abgerufen; „Jetzt abrufen“ während eines laufenden Abrufs gibt 423 zurück. Die Detailseite zeigt den nächsten geplanten Abruf.
- **Login / Session:** JWT, im Browser als HttpOnly + SameSite=Strict Cookie; API-Clients senden `Authorization: Bearer …` (`POST /api/auth/login`).
- **CSRF:** Razor-Formulare mit Antiforgery-Token; REST-Aufrufe mit Session-Cookie benötigen `X-CSRF-TOKEN` (`GET /api/auth/csrf-token`).
- **Rollen:** `Admin` (verwalten) und `User` (lesen). Bei LDAP folgt die Rolle der Gruppe `Ldap:AdminGroupDn`.
- **Weitere Härtung:** CSP ohne Inline-Skripte, Login-Sperre nach 5 Fehlversuchen, deaktivierte Benutzer verlieren sofort den Zugriff, Secrets per Data Protection verschlüsselt, SSH-Host-Key-Pinning.
- **Alarme (Seite „Alarme“, nur Admins):** Regeln mit Szenario, Geltungsbereich, Verzögerung, Wiederholintervall, Entwarnung und eigenen Empfängern. Versand per SMTP mit oder ohne Authentifizierung (`Mail:*`). Szenarien:
  | Szenario | Auslöser |
  |---|---|
  | VM nicht erreichbar | SSH-Abruf schlägt fehl |
  | Agent meldet sich nicht mehr | `generated_at` der monitoring.json älter als N Minuten |
  | Grenzwerte / Agent-Status | Status Warnung bzw. Kritisch (CPU, RAM, Disk, …) |
  | Live-Dienst nicht erreichbar | HTTP(S)/WS(S)/MQTT(S)-Check down |
  | Container gestoppt / unhealthy | Container läuft nicht, restarting, dead, unhealthy (optional sauber beendete ignorieren) |
  | Container verschwunden | früher gesehener Container fehlt |

  Ist eine VM nicht erreichbar, kommt nur der VM-Alarm; meldet sich ihr Agent nicht, nur „Agent meldet sich nicht mehr“. Status- und Container-Alarme dieser VM ruhen solange (keine Erinnerung, keine Entwarnung) und laufen weiter, sobald wieder aktuelle Daten da sind.

  Ablauf: erkannt → nach der Verzögerung erste Mail → solange der Zustand anhält alle *Wiederholung* Minuten eine Erinnerung (0 = einmalig) → behoben → Entwarnung. Beim ersten Start werden fünf Standardregeln angelegt.

## Konfigurator

```bash
cd Configurator
dotnet run
```

Konfigurationen werden in `%APPDATA%/SshSecMonitorer/Configurator/configurator.db` (Linux: `~/.config/…`) gespeichert.
Die rechte Spalte zeigt die fertige `config.yml` – kopieren, als Datei speichern oder eine bestehende `config.yml` aus der Zwischenablage importieren.

## Hinweise

- Frontend-Bibliotheken (Bootstrap 5.3, Bootstrap Icons, Apache ECharts 6) liegen lokal unter `Monitorer/wwwroot/lib` – kein CDN nötig (wichtig für interne Netze und die CSP).
- Die NuGet-Versionen in den `.csproj`-Dateien beim ersten `dotnet restore` prüfen und bei Bedarf auf den aktuellen Stand bringen.

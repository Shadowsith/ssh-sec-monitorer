# SshSecMonitorer

Custom, secure monitoring over SSH (SFTP), made up of three parts:

| Part | Technology | Purpose |
|---|---|---|
| **Monitorer** (`Monitorer/`) | ASP.NET 10, Razor Pages, REST controllers, PostgreSQL, Dapper + FluentMigrator | Periodically fetches each agent's `monitoring.json` via SFTP (over SSH), evaluates it, shows an overview and details, runs live checks (HTTP(S), WS(S), MQTT(S)), sends alert emails |
| **Agent** (`Agent/`) | Python 3.13, Docker | Runs on every Ubuntu/Debian VM, reads host and container data **read-only** and writes only its own `monitoring.json` |
| **Configurator** (`Configurator/`) | Avalonia UI (.NET 10), SQLite, Dapper + FluentMigrator | Creates the agents' `config.yml` (copy & paste / file) and stores configurations locally |

```
 ┌──────────────┐  SFTP (read, SSH) ┌──────────────────────────── VM ────────────────────────────┐
 │  Monitorer   │ ────────────────▶ │  /opt/sshsecmonitorer/agent/monitoring.json                 │
 │  + Postgres  │                   │        ▲ writes (only write access)                        │
 │              │ HTTP/WS/MQTT ───▶ │  Agent ──(internal network)──▶ docker-socket-proxy (POST=0)│
 └──────────────┘  (live checks)    │    └─ /proc, / read-only                                    │
                                    └─────────────────────────────────────────────────────────────┘
```

## Quick start: Monitorer (Docker)

```bash
cd docker-compose
./setup.sh                    # creates .env with random secrets, asks for the admin user
docker compose up -d --build
```

- By default bound only to `127.0.0.1:8080` → put a reverse proxy with HTTPS (nginx, Traefik, Caddy) in front of it.
  For a test without HTTPS: set `MONITORER_BIND=0.0.0.0` and `SECURE_COOKIE=false` in `.env`.
- Without LDAP an admin must exist: either via `ADMIN_USERNAME` / `ADMIN_PASSWORD` in `.env`
  (only created while no user exists yet) or via the **/Setup** page on first visit.
- The `dpkeys` volume holds the Data Protection key ring used to encrypt SSH keys and passwords in the database – **include it in backups**, otherwise stored secrets become unusable.

### Local development

```bash
docker run -d --name ssm-dev-postgres -e POSTGRES_PASSWORD=postgres -p 5432:5432 postgres:17-alpine
cd Monitorer
dotnet run                    # http://localhost:5001, Swagger at /swagger (Development)
```

The database is created and migrated automatically on startup (FluentMigrator).

## Installing the agent on a VM

1. Copy the `Agent/` folder to the VM, e.g. to `/opt/sshsecmonitorer/agent-src`.
2. Create the data directory – the agent container runs as UID 10001:
   ```bash
   sudo mkdir -p /opt/sshsecmonitorer/agent
   sudo chown 10001:10001 /opt/sshsecmonitorer/agent
   sudo chmod 755 /opt/sshsecmonitorer/agent
   ```
3. Generate `config.yml` with the Configurator (or adapt the template) and place it next to `docker-compose.yml`.
4. Start: `docker compose up -d --build`
5. Create the SSH user for the Monitorer – easiest with the matching script from `Monitorer/Assets/Scripts/`
   (in the Monitorer under **SSH-Zugänge → Bearbeiten → „Nutzer auf VM anlegen“** (SSH credentials → Edit → "Create user on VM"):
   choose the system, fill in the placeholders, click „Übernehmen“ (Apply), copy / download):

   | System | Script |
   |---|---|
   | Ubuntu ≥ 22.04 | `setup-sftp-user-ubuntu.sh` |
   | Arch Linux / Manjaro | `setup-sftp-user-arch.sh` |
   | Raspberry Pi OS / Raspbian ≥ 11 | `setup-sftp-user-raspios.sh` |

   `sudo bash setup-sftp-user-<system>.sh` creates the user `sshsecmonitorer`, which can only log in with a key (optionally only
   from the Monitorer's IP) and only has read-only SFTP access to the agent folder (chroot). It backs up and validates the
   `sshd_config`, starts the SSH service if needed and prints the path and host key for the Monitorer. The three scripts
   differ only in the system check, the package hint and the name of the SSH service (`ssh` / `sshd`) – when changing one,
   update all three. To do it by hand (no sudo, no Docker): the Monitorer fetches the file via **SFTP** – same port and
   same login as SSH, enabled by default in OpenSSH on Ubuntu/Debian:
   ```bash
   sudo adduser --disabled-password --gecos "" ssm-reader
   sudo -u ssm-reader mkdir -p ~ssm-reader/.ssh
   # add the Monitorer's public key, optionally restricted:
   # from="10.0.0.5",no-port-forwarding,no-agent-forwarding,no-X11-forwarding,no-pty ssh-ed25519 AAAA... monitorer
   sudo -u ssm-reader nano ~ssm-reader/.ssh/authorized_keys
   ```
   Recommended: restrict the user to pure, read-only SFTP in the agent folder – append to `/etc/ssh/sshd_config`:
   ```
   Match User ssm-reader
       ForceCommand internal-sftp -R
       ChrootDirectory /opt/sshsecmonitorer
       AllowTcpForwarding no
       X11Forwarding no
       PermitTTY no
   ```
   `/opt/sshsecmonitorer` must be owned by `root` and must not be writable by anyone else (`sudo chown root:root /opt/sshsecmonitorer && sudo chmod 755 /opt/sshsecmonitorer`).
   Validate and apply: `sudo sshd -t && sudo systemctl reload ssh`.
6. In the Monitorer, store the private key under **SSH-Zugänge** (SSH credentials) and add the VM under **Agents**. Path:
   `/opt/sshsecmonitorer/agent/monitoring.json`, with chroot `/agent/monitoring.json`. The SSH host key is pinned on the first
   fetch (TOFU) and strictly verified afterwards – for production, enter the fingerprint in advance (`ssh-keyscan host | ssh-keygen -lf -`).

Test without Docker: `python -m ssm_agent --config config.yml --once` (with `proc_path: /proc`, `rootfs_path: /`, `output_file: ./data/monitoring.json`).

### Local agent (the Monitorer host itself)

The host running the Monitorer doesn't need SSH: the agent runs in the same Compose project and writes to
the named volume `local-agent-data`, which the Monitorer mounts read-only at `/app/agents/local`.

```bash
cd docker-compose
docker compose --profile local-agent up -d --build
```

Then, under **Agents → Agent hinzufügen** (Add agent), choose the connection **Lokal** (Local) and enter `local` as the folder.
Configuration: `docker-compose/local-agent/config.yml` (same format as `Agent/config.yml`).

- Always mount the **folder**, never the file – the agent replaces `monitoring.json` atomically via rename.
- The Monitorer only reads below `Monitoring:LocalAgentRoot` (`/app/agents`), rejects symbolic links (folder and
  file) and limits the size to `Monitoring:MaxFileBytes`. Additional local agents: mount an additional volume at
  `/app/agents/<folder>:ro` (folder name `a–z 0–9 - _`).
- File missing / unreadable → status *Unreachable* (VM alert), outdated `generated_at` → *AgentStale* – same as with SFTP.

### Agent security model

- no open ports, only an `internal: true` network to the socket proxy
- Docker socket only via `tecnativa/docker-socket-proxy` with `POST=0` (read-only API: list/inspect/stats/logs)
- `/proc` and `/` mounted read-only, container `read_only`, `cap_drop: ALL`, `no-new-privileges`, non-root
- only write access: `/data/monitoring.json` (atomic via temp file + rename)

## monitoring.json (schema 1)

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

`ports` contains only host ports published externally (`-p` / `ports:`); ports that are only exposed internally are omitted, and ports bound only to `127.0.0.1` appear only with `docker.ports.include_localhost: true`. With `docker.ports.enabled: false` the key is omitted entirely and the Monitorer hides the ports.

`block_io` is the container's disk I/O (block devices according to the cgroup): `read_bps` / `write_bps` = current rate since the last measurement (`docker.interval`), `read_total` / `write_total` = total since container start, only with `docker.block_io.totals: true`.

What is tracked, how often, and how long the history stays in the file is controlled by `config.yml` (`interval`, `history_retention`, …).

## Monitorer – structure

```
Monitorer/
  Controllers/        REST (api/auth, api/agents, api/ssh-credentials, api/live-checks, api/users, api/setup, api/health)
  Pages/              Razor Pages: Login, Setup, Overview, Agents (list/detail/edit), SSH credentials, Live checks, Users
  Services/           Business logic (auth, LDAP, SFTP fetch, evaluation, alerts, live checks, …)
  Repository/         Dapper SQL, one class per table
  Data/Db|Dto|Monitoring|Result
  Migrations/         FluentMigrator
  BackgroundServices/ Agent polling, live checks
  Middleware/ Filters/ Extensions/ Assets/
```

- **Load distribution:** Every agent (and every live check) gets a fixed time offset within its interval – with 100 VMs at a 60 s interval, about 17 fetches start every 10 s instead of 100 at once, even right after a restart. Additionally limited by `Monitoring:MaxParallelPolls` (concurrent SFTP fetches, default 8) and `Monitoring:MaxPollsPerTick` (starts per tick, 0 = unlimited; the rest follow in the next tick, longest-waiting first). An agent is never fetched twice at the same time; „Jetzt abrufen“ (Fetch now) during a running fetch returns 423. The detail page shows the next scheduled fetch.
- **Login / session:** JWT, stored in the browser as an HttpOnly + SameSite=Strict cookie; API clients send `Authorization: Bearer …` (`POST /api/auth/login`).
- **CSRF:** Razor forms use an antiforgery token; REST calls with the session cookie require `X-CSRF-TOKEN` (`GET /api/auth/csrf-token`).
- **Roles:** `Admin` (manage) and `User` (read). With LDAP the role follows the group `Ldap:AdminGroupDn`.
- **Further hardening:** CSP without inline scripts, login lockout after 5 failed attempts, disabled users lose access immediately, secrets encrypted via Data Protection, SSH host key pinning.
- **Alerts („Alarme“ page, admins only):** Rules with scenario, scope, delay, repeat interval, all-clear and their own recipients. Sent via SMTP with or without authentication (`Mail:*`). Scenarios:
  | Scenario | Trigger |
  |---|---|
  | VM unreachable | SSH fetch fails |
  | Agent stopped reporting | `generated_at` of monitoring.json older than N minutes |
  | Thresholds / agent status | Status Warning or Critical (CPU, RAM, disk, …) |
  | Live service unreachable | HTTP(S)/WS(S)/MQTT(S) check down |
  | Container stopped / unhealthy | Container not running, restarting, dead, unhealthy (optionally ignore cleanly exited ones) |
  | Container disappeared | A previously seen container is missing |

  If a VM is unreachable, only the VM alert is sent; if its agent stops reporting, only "Agent stopped reporting". Status and container alerts for that VM are paused meanwhile (no reminders, no all-clear) and resume as soon as current data is available again.

  Flow: detected → first mail after the delay → a reminder every *repeat* minutes while the condition persists (0 = once only) → resolved → all-clear. Five default rules are created on first start.

## Configurator

```bash
cd Configurator
dotnet run
```

Configurations are stored in `%APPDATA%/SshSecMonitorer/Configurator/configurator.db` (Linux: `~/.config/…`).
The right-hand column shows the finished `config.yml` – copy it, save it as a file, or import an existing `config.yml` from the clipboard.

## Notes

- Frontend libraries (Bootstrap 5.3, Bootstrap Icons, Apache ECharts 6) are bundled locally under `Monitorer/wwwroot/lib` – no CDN required (important for internal networks and the CSP).
- Check the NuGet versions in the `.csproj` files on the first `dotnet restore` and update them if needed.

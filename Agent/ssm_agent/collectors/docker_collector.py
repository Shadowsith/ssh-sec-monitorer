"""Reads container state, stats and logs via the read-only Docker proxy."""

from __future__ import annotations

import fnmatch
import logging
import re
import time
from concurrent.futures import ThreadPoolExecutor
from typing import Any

from ..config import DockerSection
from ..history import MetricHistory

log = logging.getLogger(__name__)


class DockerCollector:
    """Collects container information.

    Uses GET requests only (list, inspect, stats, logs).
    """

    def __init__(self, cfg: DockerSection) -> None:
        self._cfg = cfg
        self._client = None
        self._history = MetricHistory(
            cfg.history_retention if cfg.history else 0
        )
        self._prev_cpu: dict[str, tuple[int, int]] = {}
        self._prev_io: dict[str, tuple[float, int, int]] = {}
        self._containers: list[dict[str, Any]] = []
        self._logs: dict[str, dict[str, Any]] = {}
        self._error: str | None = None
        self._engine: dict[str, Any] = {}
        self._error_re = re.compile(cfg.logs.error_pattern)
        self._warning_re = re.compile(cfg.logs.warning_pattern)

    @property
    def interval(self) -> int:
        return self._cfg.interval

    @property
    def log_interval(self) -> int:
        return self._cfg.logs.interval

    # ---------------------------------------------------------------- public

    def collect(self) -> None:
        try:
            client = self._get_client()
            containers = [
                c
                for c in client.containers.list(all=True)
                if self._include(c.name)
            ]
            now = int(time.time())
            stats: dict[str, dict[str, Any]] = {}
            running = [c for c in containers if c.status == "running"]
            if self._cfg.stats and running:
                with ThreadPoolExecutor(
                    max_workers=max(1, self._cfg.max_workers)
                ) as pool:
                    for cid, result in pool.map(self._read_stats, running):
                        if result:
                            stats[cid] = result
            self._containers = [
                self._describe(c, stats.get(c.id), now) for c in containers
            ]
            ids = {c.id for c in containers}
            self._history.remove_missing({c["name"] for c in self._containers})
            self._prev_cpu = {
                k: v for k, v in self._prev_cpu.items() if k in ids
            }
            self._prev_io = {
                k: v for k, v in self._prev_io.items() if k in ids
            }
            self._logs = {k: v for k, v in self._logs.items() if k in ids}
            self._error = None
        except Exception as exc:  # docker unreachable, proxy down, ...
            log.warning("Docker collection failed: %s", exc)
            self._error = str(exc)
            self._client = None

    def collect_logs(self) -> None:
        if not self._cfg.logs.enabled or self._client is None:
            return
        try:
            containers = self._client.containers.list(all=True)
        except Exception as exc:
            log.warning("Docker log collection failed: %s", exc)
            return
        for container in containers:
            if not self._include(container.name):
                continue
            try:
                self._logs[container.id] = self._read_logs(container)
            except Exception as exc:
                # e.g. 501 for logging drivers without read support (none, syslog, gelf, ...)
                # - one unreadable container must not stop log collection for the others
                self._logs.pop(container.id, None)
                log.debug("Docker logs for %s not readable: %s", container.name, exc)

    def snapshot(self) -> dict[str, Any]:
        containers = []
        for c in self._containers:
            entry = dict(c)
            entry["logs"] = self._logs.get(c["id"])
            containers.append(entry)
        return {
            "available": self._error is None and self._client is not None,
            "error": self._error,
            "engine": self._engine,
            "containers": containers,
            "history": self._history.export(),
        }

    # ------------------------------------------------------------ internals

    def _get_client(self):
        if self._client is None:
            # imported lazily so the agent also runs with docker disabled
            import docker

            self._client = docker.DockerClient(
                base_url=self._cfg.base_url, timeout=self._cfg.timeout
            )
            version = self._client.version()
            self._engine = {
                "version": version.get("Version"),
                "api_version": version.get("ApiVersion"),
            }
        return self._client

    def _include(self, name: str) -> bool:
        if self._cfg.include and not any(
            fnmatch.fnmatch(name, p) for p in self._cfg.include
        ):
            return False
        return not any(fnmatch.fnmatch(name, p) for p in self._cfg.exclude)

    def _read_stats(self, container) -> tuple[str, dict[str, Any] | None]:
        try:
            return container.id, container.stats(stream=False, one_shot=True)
        except Exception as exc:
            log.debug("stats failed for %s: %s", container.name, exc)
            return container.id, None

    def _cpu_percent(self, cid: str, stats: dict[str, Any]) -> float | None:
        cpu = stats.get("cpu_stats") or {}
        total = (cpu.get("cpu_usage") or {}).get("total_usage")
        system = cpu.get("system_cpu_usage")
        if total is None or system is None:
            return None
        online = (
            cpu.get("online_cpus")
            or len((cpu.get("cpu_usage") or {}).get("percpu_usage") or [])
            or 1
        )
        prev, self._prev_cpu[cid] = self._prev_cpu.get(cid), (total, system)
        if prev is None or system <= prev[1] or total < prev[0]:
            return None
        return round(
            (total - prev[0]) / (system - prev[1]) * online * 100.0, 1
        )

    @staticmethod
    def _memory(stats: dict[str, Any]) -> dict[str, Any]:
        mem = stats.get("memory_stats") or {}
        usage = mem.get("usage")
        limit = mem.get("limit")
        detail = mem.get("stats") or {}
        cache = detail.get(
            "inactive_file", detail.get("total_inactive_file", 0)
        )
        if usage is not None:
            usage = max(0, usage - cache)
        return {
            "usage": usage,
            "limit": limit,
            "percent": (
                round(100.0 * usage / limit, 1)
                if usage is not None and limit
                else None
            ),
        }

    @staticmethod
    def _network(stats: dict[str, Any]) -> dict[str, int]:
        nets = stats.get("networks") or {}
        return {
            "rx_total": sum(n.get("rx_bytes", 0) for n in nets.values()),
            "tx_total": sum(n.get("tx_bytes", 0) for n in nets.values()),
        }

    def _describe(
        self, container, stats: dict[str, Any] | None, now: int
    ) -> dict[str, Any]:
        attrs = container.attrs or {}
        state = attrs.get("State") or {}
        entry: dict[str, Any] = {
            "id": container.id,
            "name": container.name,
            "image": (attrs.get("Config") or {}).get("Image"),
            "state": state.get("Status", container.status),
            "health": (state.get("Health") or {}).get("Status"),
            "exit_code": state.get("ExitCode"),
            "restart_count": attrs.get("RestartCount"),
            "started_at": state.get("StartedAt"),
            "cpu_percent": None,
            "memory": None,
            "network": None,
        }
        if self._cfg.ports.enabled:
            entry["ports"] = self._published_ports(attrs)
        if stats:
            entry["cpu_percent"] = self._cpu_percent(container.id, stats)
            entry["memory"] = self._memory(stats)
            entry["network"] = self._network(stats)
            self._history.add(
                f"{container.name}.cpu_percent", now, entry["cpu_percent"]
            )
            self._history.add(
                f"{container.name}.memory_percent",
                now,
                entry["memory"]["percent"],
            )
            if self._cfg.block_io.enabled:
                entry["block_io"] = self._block_io(container.id, stats)
                self._history.add(
                    f"{container.name}.io_read_bps",
                    now,
                    entry["block_io"]["read_bps"],
                )
                self._history.add(
                    f"{container.name}.io_write_bps",
                    now,
                    entry["block_io"]["write_bps"],
                )
        return entry

    def _block_io(self, cid: str, stats: dict[str, Any]) -> dict[str, Any]:
        """Read / write bytes on block devices.

        cgroup v1 reports "Read" / "Write", cgroup v2 "read" / "write".
        """
        entries = (stats.get("blkio_stats") or {}).get(
            "io_service_bytes_recursive"
        ) or []
        read = sum(
            e.get("value", 0)
            for e in entries
            if str(e.get("op", "")).lower() == "read"
        )
        write = sum(
            e.get("value", 0)
            for e in entries
            if str(e.get("op", "")).lower() == "write"
        )
        now = time.monotonic()
        prev, self._prev_io[cid] = self._prev_io.get(cid), (now, read, write)
        read_bps = write_bps = None
        if prev and now > prev[0] and read >= prev[1] and write >= prev[2]:
            elapsed = now - prev[0]
            read_bps = round((read - prev[1]) / elapsed, 1)
            write_bps = round((write - prev[2]) / elapsed, 1)
        result: dict[str, Any] = {
            "read_bps": read_bps,
            "write_bps": write_bps,
            "available": bool(entries),
        }
        if self._cfg.block_io.totals:
            result["read_total"] = read
            result["write_total"] = write
        return result

    LOOPBACK = {"127.0.0.1", "::1"}

    def _published_ports(self, attrs: dict[str, Any]) -> list[dict[str, Any]]:
        """Host ports published to the outside.

        Internal ports (only EXPOSEd, not published) are skipped.
        """
        mapping = (attrs.get("NetworkSettings") or {}).get("Ports") or {}
        seen: set[tuple[int, int, str, bool]] = set()
        result = []
        for container_port, bindings in mapping.items():
            if not bindings:  # exposed but not published
                continue
            port, _, protocol = container_port.partition("/")
            for binding in bindings:
                host_ip = binding.get("HostIp") or "0.0.0.0"
                host_port = binding.get("HostPort")
                if not host_port:
                    continue
                localhost = host_ip in self.LOOPBACK
                if localhost and not self._cfg.ports.include_localhost:
                    continue
                key = (int(host_port), int(port), protocol or "tcp", localhost)
                if key in seen:  # same port on IPv4 and IPv6 (0.0.0.0 and ::)
                    continue
                seen.add(key)
                result.append(
                    {
                        "host_ip": (
                            "0.0.0.0" if host_ip in ("", "::") else host_ip
                        ),
                        "host_port": int(host_port),
                        "container_port": int(port),
                        "protocol": protocol or "tcp",
                        "localhost_only": localhost,
                    }
                )
        return sorted(result, key=lambda p: (p["host_port"], p["protocol"]))

    def _read_logs(self, container) -> dict[str, Any]:
        cfg = self._cfg.logs
        now = int(time.time())
        raw = container.logs(
            stdout=True,
            stderr=True,
            timestamps=True,
            since=now - cfg.window,
            tail=cfg.max_scan_lines,
        )
        lines = raw.decode("utf-8", errors="replace").splitlines()
        errors = sum(1 for line in lines if self._error_re.search(line))
        warnings = sum(1 for line in lines if self._warning_re.search(line))
        max_len = cfg.max_line_length
        first = max(len(lines) - cfg.tail_lines, 0)
        tail = (
            [line[:max_len] for line in lines[first:]]
            if cfg.tail_lines
            else []
        )
        return {
            "window_seconds": cfg.window,
            "scanned_lines": len(lines),
            "error_count": errors,
            "warning_count": warnings,
            "tail": tail,
            "collected_at": now,
        }

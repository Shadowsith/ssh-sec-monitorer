"""Reads CPU, RAM, load, disk and network usage from a read-only /proc."""

from __future__ import annotations

import fnmatch
import logging
import os
import time
from pathlib import Path
from typing import Any

from ..config import HostSection
from ..history import MetricHistory

log = logging.getLogger(__name__)


class HostCollector:
    """Collects host metrics.

    Only reads files - never writes anything on the host.
    """

    REAL_FILESYSTEMS = {
        "ext2",
        "ext3",
        "ext4",
        "xfs",
        "btrfs",
        "zfs",
        "vfat",
        "exfat",
        "ntfs",
        "f2fs",
        "reiserfs",
        "jfs",
    }

    def __init__(self, cfg: HostSection) -> None:
        self._cfg = cfg
        self._proc = Path(cfg.proc_path)
        self._rootfs = Path(cfg.rootfs_path)
        self._history = MetricHistory(cfg.history_retention)
        self._prev_cpu: tuple[int, int] | None = None
        self._prev_net: dict[str, tuple[int, int]] = {}
        self._prev_net_ts: float | None = None
        self._current: dict[str, Any] = {}

    @property
    def interval(self) -> int:
        return self._cfg.interval

    def collect(self) -> None:
        now = int(time.time())
        current: dict[str, Any] = {
            "hostname": self._read_hostname(),
            "uptime_seconds": self._read_uptime(),
        }
        if self._cfg.cpu:
            current["cpu_percent"] = self._read_cpu_percent()
            current["cpu_count"] = self._read_cpu_count()
            self._history.add("cpu_percent", now, current["cpu_percent"])
        if self._cfg.load:
            current["load"] = self._read_load()
        if self._cfg.memory:
            current["memory"], current["swap"] = self._read_memory()
            self._history.add(
                "memory_percent", now, current["memory"].get("percent")
            )
        if self._cfg.disks.enabled:
            current["disks"] = self._read_disks()
            for disk in current["disks"]:
                self._history.add(
                    f"disk_percent:{disk['mount']}", now, disk.get("percent")
                )
        if self._cfg.network.enabled:
            current["network"] = self._read_network()
            rated = [n for n in current["network"] if n["rx_bps"] is not None]
            if rated:
                rx = sum(n["rx_bps"] for n in rated)
                tx = sum(n["tx_bps"] for n in rated)
                self._history.add("net_rx_bps", now, rx)
                self._history.add("net_tx_bps", now, tx)
        current["collected_at"] = now
        self._current = current

    def snapshot(self) -> dict[str, Any]:
        return {"current": self._current, "history": self._history.export()}

    # ---------------------------------------------------------------- readers

    def _read(self, *parts: str) -> str | None:
        try:
            return self._proc.joinpath(*parts).read_text(
                encoding="utf-8", errors="replace"
            )
        except OSError:
            return None

    def _read_hostname(self) -> str | None:
        value = self._read("sys", "kernel", "hostname")
        return value.strip() if value else None

    def _read_uptime(self) -> float | None:
        value = self._read("uptime")
        return round(float(value.split()[0]), 0) if value else None

    def _read_cpu_count(self) -> int | None:
        stat = self._read("stat")
        if not stat:
            return None
        return sum(
            1
            for line in stat.splitlines()
            if line.startswith("cpu") and line[3:4].isdigit()
        )

    def _read_cpu_percent(self) -> float | None:
        stat = self._read("stat")
        if not stat:
            return None
        fields = [int(v) for v in stat.splitlines()[0].split()[1:9]]
        idle = fields[3] + fields[4]  # idle + iowait
        total = sum(fields)
        prev, self._prev_cpu = self._prev_cpu, (idle, total)
        if prev is None or total <= prev[1]:
            return None
        return round(100.0 * (1 - (idle - prev[0]) / (total - prev[1])), 1)

    def _read_load(self) -> list[float] | None:
        value = self._read("loadavg")
        return [float(v) for v in value.split()[:3]] if value else None

    def _read_memory(self) -> tuple[dict[str, Any], dict[str, Any]]:
        info: dict[str, int] = {}
        for line in (self._read("meminfo") or "").splitlines():
            key, _, rest = line.partition(":")
            parts = rest.split()
            if parts:
                info[key] = int(parts[0]) * 1024
        total = info.get("MemTotal", 0)
        available = info.get("MemAvailable", info.get("MemFree", 0))
        used = total - available
        swap_total = info.get("SwapTotal", 0)
        swap_used = swap_total - info.get("SwapFree", 0)
        memory = {
            "total": total,
            "used": used,
            "available": available,
            "percent": round(100.0 * used / total, 1) if total else None,
        }
        swap = {
            "total": swap_total,
            "used": swap_used,
            "percent": (
                round(100.0 * swap_used / swap_total, 1)
                if swap_total
                else None
            ),
        }
        return memory, swap

    def _discover_mounts(self) -> list[str]:
        mounts = self._read("1", "mounts") or self._read("mounts") or ""
        result: list[str] = []
        for line in mounts.splitlines():
            parts = line.split()
            if (
                len(parts) >= 3
                and parts[2] in self.REAL_FILESYSTEMS
                and parts[1] not in result
            ):
                result.append(parts[1].replace("\\040", " "))
        return result or ["/"]

    def _read_disks(self) -> list[dict[str, Any]]:
        disks = []
        for mount in self._cfg.disks.mounts or self._discover_mounts():
            path = self._rootfs / mount.lstrip("/")
            try:
                st = os.statvfs(path)
            except OSError as exc:
                log.debug("statvfs failed for %s: %s", path, exc)
                continue
            total = st.f_blocks * st.f_frsize
            free = st.f_bavail * st.f_frsize
            used = total - st.f_bfree * st.f_frsize
            disks.append(
                {
                    "mount": mount,
                    "total": total,
                    "used": used,
                    "free": free,
                    "percent": (
                        round(100.0 * used / (used + free), 1)
                        if (used + free)
                        else None
                    ),
                }
            )
        return disks

    def _include_interface(self, name: str) -> bool:
        net = self._cfg.network
        if net.interfaces:
            return name in net.interfaces
        return not any(
            fnmatch.fnmatch(name, pattern) for pattern in net.exclude
        )

    def _read_network(self) -> list[dict[str, Any]]:
        # /proc/1/net/dev = network namespace of the host's init process
        # (the host, not the container)
        content = (
            self._read("1", "net", "dev") or self._read("net", "dev") or ""
        )
        now = time.monotonic()
        elapsed = (now - self._prev_net_ts) if self._prev_net_ts else None
        counters: dict[str, tuple[int, int]] = {}
        result = []
        for line in content.splitlines()[2:]:
            name, _, data = line.partition(":")
            name = name.strip()
            if not self._include_interface(name):
                continue
            values = data.split()
            rx, tx = int(values[0]), int(values[8])
            counters[name] = (rx, tx)
            prev = self._prev_net.get(name)
            rx_bps = tx_bps = None
            if prev and elapsed and rx >= prev[0] and tx >= prev[1]:
                rx_bps = round((rx - prev[0]) / elapsed, 1)
                tx_bps = round((tx - prev[1]) / elapsed, 1)
            result.append(
                {
                    "interface": name,
                    "rx_total": rx,
                    "tx_total": tx,
                    "rx_bps": rx_bps,
                    "tx_bps": tx_bps,
                }
            )
        self._prev_net = counters
        self._prev_net_ts = now
        return result

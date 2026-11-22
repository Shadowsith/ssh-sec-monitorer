"""Loads and validates config.yml for the agent."""

from __future__ import annotations

import copy
import re
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any

import yaml


@dataclass
class AgentSection:
    name: str = "agent"
    output_file: str = "/data/monitoring.json"
    output_file_mode: str = "0644"
    write_interval: int = 15


@dataclass
class DiskSection:
    enabled: bool = True
    # Host mount points (e.g. "/", "/var/lib/docker").
    # Empty = auto-discover real filesystems.
    mounts: list[str] = field(default_factory=list)


@dataclass
class NetworkSection:
    enabled: bool = True
    # Interface names to track.
    # Empty = all interfaces except the excluded ones.
    interfaces: list[str] = field(default_factory=list)
    exclude: list[str] = field(
        default_factory=lambda: ["lo", "veth*", "docker*", "br-*"]
    )


@dataclass
class HostSection:
    enabled: bool = True
    proc_path: str = "/host/proc"
    rootfs_path: str = "/hostfs"
    interval: int = 10
    history_retention: int = 3600
    cpu: bool = True
    memory: bool = True
    load: bool = True
    disks: DiskSection = field(default_factory=DiskSection)
    network: NetworkSection = field(default_factory=NetworkSection)


@dataclass
class DockerLogSection:
    enabled: bool = True
    interval: int = 60
    tail_lines: int = 20
    window: int = 300
    max_scan_lines: int = 1000
    max_line_length: int = 500
    error_pattern: str = r"(?i)\b(error|fatal|panic|exception|critical)\b"
    warning_pattern: str = r"(?i)\bwarn(ing)?\b"


@dataclass
class DockerPortSection:
    # Report the host ports a container publishes (docker run -p ...).
    enabled: bool = True
    # Also list ports bound only to loopback (127.0.0.1 / ::1);
    # these are not reachable from outside.
    include_localhost: bool = False


@dataclass
class DockerBlockIoSection:
    # Disk I/O per container (block devices): read / write rate in bytes/s.
    enabled: bool = True
    # Also report the cumulative read / write bytes since container start.
    totals: bool = True


@dataclass
class DockerSection:
    enabled: bool = True
    base_url: str = "tcp://docker-proxy:2375"
    timeout: int = 10
    interval: int = 30
    stats: bool = True
    history: bool = True
    history_retention: int = 3600
    max_workers: int = 4
    include: list[str] = field(default_factory=list)
    exclude: list[str] = field(default_factory=list)
    ports: DockerPortSection = field(default_factory=DockerPortSection)
    block_io: DockerBlockIoSection = field(
        default_factory=DockerBlockIoSection
    )
    logs: DockerLogSection = field(default_factory=DockerLogSection)


@dataclass
class AgentConfig:
    agent: AgentSection = field(default_factory=AgentSection)
    host: HostSection = field(default_factory=HostSection)
    docker: DockerSection = field(default_factory=DockerSection)


class ConfigError(Exception):
    """Raised when config.yml contains invalid values."""


class ConfigLoader:
    """Reads config.yml, merges it over the defaults and validates it."""

    MIN_INTERVAL = 1

    @classmethod
    def load(cls, path: str | Path) -> AgentConfig:
        raw: dict[str, Any] = {}
        p = Path(path)
        if p.exists():
            with p.open("r", encoding="utf-8") as fh:
                raw = yaml.safe_load(fh) or {}
        if not isinstance(raw, dict):
            raise ConfigError("config.yml must contain a mapping at top level")

        config = AgentConfig(
            agent=cls._build(AgentSection, raw.get("agent")),
            host=cls._build_host(raw.get("host")),
            docker=cls._build_docker(raw.get("docker")),
        )
        cls._validate(config)
        return config

    @classmethod
    def _build_host(cls, raw: Any) -> HostSection:
        raw = copy.deepcopy(raw or {})
        disks = cls._build(DiskSection, raw.pop("disks", None))
        network = cls._build(NetworkSection, raw.pop("network", None))
        host = cls._build(HostSection, raw)
        host.disks = disks
        host.network = network
        return host

    @classmethod
    def _build_docker(cls, raw: Any) -> DockerSection:
        raw = copy.deepcopy(raw or {})
        logs = cls._build(DockerLogSection, raw.pop("logs", None))
        ports = cls._build(DockerPortSection, raw.pop("ports", None))
        block_io = cls._build(DockerBlockIoSection, raw.pop("block_io", None))
        docker = cls._build(DockerSection, raw)
        docker.block_io = block_io
        docker.logs = logs
        docker.ports = ports
        return docker

    @staticmethod
    def _build(section_type: type, raw: Any):
        if raw is None:
            return section_type()
        if not isinstance(raw, dict):
            raise ConfigError(
                f"Section for {section_type.__name__} must be a mapping"
            )
        known = set(section_type.__dataclass_fields__.keys())
        unknown = set(raw.keys()) - known
        if unknown:
            raise ConfigError(
                f"Unknown keys in {section_type.__name__}: "
                f"{', '.join(sorted(unknown))}"
            )
        return section_type(**raw)

    @classmethod
    def _validate(cls, config: AgentConfig) -> None:
        intervals = {
            "agent.write_interval": config.agent.write_interval,
            "host.interval": config.host.interval,
            "docker.interval": config.docker.interval,
            "docker.logs.interval": config.docker.logs.interval,
        }
        for key, value in intervals.items():
            if not isinstance(value, int) or value < cls.MIN_INTERVAL:
                raise ConfigError(
                    f"{key} must be an integer >= {cls.MIN_INTERVAL}"
                )
        for key, value in {
            "host.history_retention": config.host.history_retention,
            "docker.history_retention": config.docker.history_retention,
        }.items():
            if not isinstance(value, int) or value < 0:
                raise ConfigError(f"{key} must be an integer >= 0")
        try:
            int(config.agent.output_file_mode, 8)
        except (TypeError, ValueError) as exc:
            raise ConfigError(
                "agent.output_file_mode must be an octal string like '0644'"
            ) from exc
        for key, pattern in {
            "docker.logs.error_pattern": config.docker.logs.error_pattern,
            "docker.logs.warning_pattern": config.docker.logs.warning_pattern,
        }.items():
            try:
                re.compile(pattern)
            except re.error as exc:
                raise ConfigError(
                    f"{key} is not a valid regular expression: {exc}"
                ) from exc

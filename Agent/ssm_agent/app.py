"""Main loop: runs the collectors on their intervals, writes the JSON file."""

from __future__ import annotations

import argparse
import logging
import signal
import threading
import time
from datetime import datetime, timezone
from typing import Callable

from . import __version__
from .collectors.docker_collector import DockerCollector
from .collectors.host_collector import HostCollector
from .config import AgentConfig, ConfigLoader
from .writer import MonitoringFileWriter

log = logging.getLogger("ssm_agent")

SCHEMA_VERSION = 1


class ScheduledJob:
    """A callable that runs every `interval` seconds."""

    def __init__(
        self, name: str, interval: int, action: Callable[[], None]
    ) -> None:
        self.name = name
        self.interval = interval
        self.action = action
        self.next_run = 0.0

    def run_if_due(self, now: float) -> None:
        if now < self.next_run:
            return
        self.next_run = now + self.interval
        try:
            self.action()
        except Exception:
            log.exception("Job %s failed", self.name)


class AgentApp:
    """Wires config, collectors and writer together."""

    def __init__(self, config: AgentConfig) -> None:
        self._config = config
        self._stop = threading.Event()
        self._writer = MonitoringFileWriter(
            config.agent.output_file, config.agent.output_file_mode
        )
        self._host = (
            HostCollector(config.host) if config.host.enabled else None
        )
        self._docker = (
            DockerCollector(config.docker) if config.docker.enabled else None
        )
        self._jobs: list[ScheduledJob] = []
        if self._host:
            self._jobs.append(
                ScheduledJob("host", self._host.interval, self._host.collect)
            )
        if self._docker:
            self._jobs.append(
                ScheduledJob(
                    "docker", self._docker.interval, self._docker.collect
                )
            )
            if config.docker.logs.enabled:
                self._jobs.append(
                    ScheduledJob(
                        "docker-logs",
                        self._docker.log_interval,
                        self._docker.collect_logs,
                    )
                )
        self._jobs.append(
            ScheduledJob("write", config.agent.write_interval, self.write)
        )

    @staticmethod
    def main() -> None:
        parser = argparse.ArgumentParser(description="SshSecMonitorer agent")
        parser.add_argument("--config", default="/config/config.yml")
        parser.add_argument(
            "--once",
            action="store_true",
            help="collect twice (for rates), write once and exit",
        )
        parser.add_argument("--log-level", default="INFO")
        args = parser.parse_args()
        logging.basicConfig(
            level=args.log_level.upper(),
            format="%(asctime)s %(levelname)s %(name)s: %(message)s",
        )

        app = AgentApp(ConfigLoader.load(args.config))
        if args.once:
            app.run_once()
            return
        signal.signal(signal.SIGTERM, lambda *_: app.stop())
        signal.signal(signal.SIGINT, lambda *_: app.stop())
        app.run()

    def stop(self) -> None:
        self._stop.set()

    def run(self) -> None:
        log.info(
            "Agent %s started (version %s), writing to %s",
            self._config.agent.name,
            __version__,
            self._config.agent.output_file,
        )
        # The first write is delayed by one host interval so that CPU and
        # network rates are available.
        write_job = self._jobs[-1]
        write_job.next_run = time.monotonic() + min(
            self._config.agent.write_interval,
            self._config.host.interval if self._host else 0,
        )
        while not self._stop.is_set():
            now = time.monotonic()
            for job in self._jobs:
                job.run_if_due(now)
            next_run = min(job.next_run for job in self._jobs)
            self._stop.wait(max(0.2, next_run - time.monotonic()))
        log.info("Agent stopped")

    def run_once(self) -> None:
        for _ in range(2):
            if self._host:
                self._host.collect()
            if self._docker:
                self._docker.collect()
            time.sleep(1)
        if self._docker:
            self._docker.collect_logs()
        self.write()

    def write(self) -> None:
        data = {
            "schema_version": SCHEMA_VERSION,
            "generated_at": datetime.now(timezone.utc).strftime(
                "%Y-%m-%dT%H:%M:%SZ"
            ),
            "agent": {
                "name": self._config.agent.name,
                "version": __version__,
                "intervals": {
                    "write": self._config.agent.write_interval,
                    "host": self._config.host.interval if self._host else None,
                    "docker": (
                        self._config.docker.interval if self._docker else None
                    ),
                },
            },
            "host": self._host.snapshot() if self._host else None,
            "docker": self._docker.snapshot() if self._docker else None,
        }
        size = self._writer.write(data)
        log.debug("monitoring.json written (%d bytes)", size)

"""Time series kept in memory and written into monitoring.json."""

from __future__ import annotations

from collections import deque


class MetricHistory:
    """Named series of [unix_ts, value] points, trimmed to a retention."""

    def __init__(self, retention_seconds: int) -> None:
        self._retention = retention_seconds
        self._series: dict[str, deque[tuple[int, float]]] = {}

    def add(self, name: str, ts: int, value: float | None) -> None:
        if value is None or self._retention <= 0:
            return
        series = self._series.setdefault(name, deque())
        series.append((ts, round(float(value), 2)))
        self._trim(series, ts)

    def remove_missing(self, keep: set[str], prefix: str = "") -> None:
        """Drops series whose name (after prefix) is not in keep.

        Used e.g. for containers that were removed.
        """
        start = len(prefix)
        for name in list(self._series.keys()):
            if (
                name.startswith(prefix)
                and name[start:].split(".")[0] not in keep
            ):
                del self._series[name]

    def export(self, prefix: str = "") -> dict[str, list[list[float]]]:
        start = len(prefix)
        return {
            name[start:]: [[ts, value] for ts, value in series]
            for name, series in self._series.items()
            if name.startswith(prefix)
        }

    def _trim(self, series: deque[tuple[int, float]], now: int) -> None:
        border = now - self._retention
        while series and series[0][0] < border:
            series.popleft()

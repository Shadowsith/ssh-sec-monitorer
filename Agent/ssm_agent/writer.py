"""Writes monitoring.json atomically (temp file + rename).

Readers therefore never see a half-written file.
"""

from __future__ import annotations

import json
import os
import tempfile
from pathlib import Path
from typing import Any


class MonitoringFileWriter:
    """The only place where the agent writes to disk."""

    def __init__(self, output_file: str, file_mode: str) -> None:
        self._path = Path(output_file)
        self._mode = int(file_mode, 8)

    def write(self, data: dict[str, Any]) -> int:
        payload = json.dumps(
            data, separators=(",", ":"), ensure_ascii=False
        ).encode("utf-8")
        self._path.parent.mkdir(parents=True, exist_ok=True)
        fd, tmp = tempfile.mkstemp(
            prefix=".monitoring-", suffix=".tmp", dir=self._path.parent
        )
        try:
            with os.fdopen(fd, "wb") as fh:
                fh.write(payload)
                fh.flush()
                os.fsync(fh.fileno())
            os.chmod(tmp, self._mode)
            os.replace(tmp, self._path)
        except BaseException:
            Path(tmp).unlink(missing_ok=True)
            raise
        return len(payload)

"""PostgreSQL NOTIFY event publisher for the Python agent runtime.

Publishes events to PostgreSQL channels so the .NET EventBus can receive them.
This is the Python-side counterpart to PostgresEventBus.cs on the .NET side.
"""

from __future__ import annotations

import json
import logging
import os
from typing import Any

logger = logging.getLogger(__name__)

# Optional: psycopg is only needed when actually publishing
try:
    import psycopg
    HAS_PSYCOPG = True
except ImportError:
    HAS_PSYCOPG = False


class PostgresEventPublisher:
    """Publishes NOTIFY events to PostgreSQL channels.

    Uses psycopg (async) to send notifications that the .NET
    PostgresEventBus picks up via LISTEN.
    """

    def __init__(self, database_url: str | None = None) -> None:
        self._database_url = database_url or os.getenv("DATABASE_URL", "")
        self._conn: Any | None = None

    @property
    def is_available(self) -> bool:
        """Check if postgres publishing is available."""
        return HAS_PSYCOPG and bool(self._database_url)

    async def connect(self) -> None:
        """Establish connection to PostgreSQL."""
        if not HAS_PSYCOPG:
            logger.warning("psycopg not installed, event publishing disabled")
            return
        if not self._database_url:
            logger.warning("DATABASE_URL not set, event publishing disabled")
            return

        try:
            self._conn = await psycopg.AsyncConnection.connect(
                self._database_url, autocommit=True
            )
            logger.info("PostgresEventPublisher connected")
        except Exception as e:
            logger.error("Failed to connect to PostgreSQL for events: %s", e)
            self._conn = None

    async def publish(self, channel: str, payload: str) -> None:
        """Publish a NOTIFY event to a PostgreSQL channel.

        Args:
            channel: Channel name (alphanumeric + underscores only).
            payload: JSON string payload (max ~8000 bytes for PostgreSQL).
        """
        if self._conn is None:
            return

        sanitized = _sanitize_channel(channel)
        try:
            await self._conn.execute(f"NOTIFY {sanitized}, '{_escape(payload)}'")
            logger.debug("Published to %s: %d chars", sanitized, len(payload))
        except Exception as e:
            logger.error("Failed to publish to %s: %s", sanitized, e)
            # Try to reconnect on next publish
            self._conn = None

    async def publish_event(
        self,
        channel: str,
        event_type: str,
        data: dict[str, Any],
    ) -> None:
        """Publish a structured event.

        Args:
            channel: PostgreSQL channel name.
            event_type: Event type (e.g., "task.completed").
            data: Event data dict (will be merged with type field).
        """
        payload = {**data, "type": event_type}
        await self.publish(channel, json.dumps(payload))

    async def close(self) -> None:
        """Close the PostgreSQL connection."""
        if self._conn is not None:
            await self._conn.close()
            self._conn = None
            logger.info("PostgresEventPublisher disconnected")


def _sanitize_channel(channel: str) -> str:
    """Sanitize channel name for PostgreSQL (alphanumeric + underscores)."""
    return "".join(c for c in channel if c.isalnum() or c == "_").lower()


def _escape(payload: str) -> str:
    """Escape single quotes for PostgreSQL NOTIFY payload."""
    return payload.replace("'", "''")

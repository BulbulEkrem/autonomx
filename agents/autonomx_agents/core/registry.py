"""Agent registry — register and discover agents."""

from __future__ import annotations

import logging
from typing import TYPE_CHECKING

if TYPE_CHECKING:
    from autonomx_agents.core.base_agent import BaseAgent

logger = logging.getLogger(__name__)

_registry: dict[str, type[BaseAgent]] = {}


def agent_register(name: str):
    """Decorator to register an agent class."""

    def decorator(cls: type[BaseAgent]) -> type[BaseAgent]:
        _registry[name] = cls
        logger.info("Registered agent: %s -> %s", name, cls.__name__)
        return cls

    return decorator


def get_agent_class(name: str) -> type[BaseAgent] | None:
    """Get a registered agent class by name."""
    return _registry.get(name)


def list_agents() -> list[str]:
    """List all registered agent names."""
    return list(_registry.keys())

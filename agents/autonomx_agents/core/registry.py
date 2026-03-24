"""Agent registry — register, discover, and instantiate agents."""

from __future__ import annotations

import importlib
import logging
import pkgutil
from pathlib import Path
from typing import TYPE_CHECKING

if TYPE_CHECKING:
    from autonomx_agents.core.base_agent import BaseAgent
    from autonomx_agents.core.config import AgentConfig

logger = logging.getLogger(__name__)

_registry: dict[str, type[BaseAgent]] = {}


def agent_register(name: str):
    """Decorator to register an agent class.

    Usage:
        @agent_register("planner")
        class PlannerAgent(BaseAgent):
            ...
    """

    def decorator(cls: type[BaseAgent]) -> type[BaseAgent]:
        if name in _registry:
            logger.warning(
                "Overriding existing agent registration: %s (%s -> %s)",
                name,
                _registry[name].__name__,
                cls.__name__,
            )
        _registry[name] = cls
        logger.info("Registered agent: %s -> %s", name, cls.__name__)
        return cls

    return decorator


def get_agent_class(name: str) -> type[BaseAgent] | None:
    """Get a registered agent class by name."""
    return _registry.get(name)


def create_agent(config: AgentConfig) -> BaseAgent:
    """Create an agent instance from config.

    Raises:
        ValueError: If agent type is not registered.
    """
    cls = _registry.get(config.type)
    if cls is None:
        raise ValueError(
            f"Unknown agent type: '{config.type}'. "
            f"Registered types: {list(_registry.keys())}"
        )
    return cls(config)


def list_agents() -> list[str]:
    """List all registered agent type names."""
    return list(_registry.keys())


def discover_agents(package_name: str = "autonomx_agents.agents") -> int:
    """Auto-discover and import agent modules from the agents package.

    Returns the number of newly discovered agent types.
    """
    before = len(_registry)

    try:
        package = importlib.import_module(package_name)
    except ImportError:
        logger.warning("Could not import package: %s", package_name)
        return 0

    if not hasattr(package, "__path__"):
        return 0

    for _importer, module_name, _is_pkg in pkgutil.walk_packages(
        package.__path__,
        prefix=f"{package_name}.",
    ):
        try:
            importlib.import_module(module_name)
            logger.debug("Imported agent module: %s", module_name)
        except Exception as e:
            logger.warning("Failed to import agent module %s: %s", module_name, e)

    discovered = len(_registry) - before
    if discovered > 0:
        logger.info("Discovered %d new agent type(s) from %s", discovered, package_name)
    return discovered


def discover_plugins(plugins_dir: str | Path = "plugins") -> int:
    """Auto-discover and import agent plugins from the plugins directory.

    Plugins are Python modules/packages in the plugins/ directory that use
    @agent_register to register themselves.

    Returns the number of newly discovered agent types.
    """
    before = len(_registry)
    plugins_path = Path(plugins_dir)

    if not plugins_path.exists() or not plugins_path.is_dir():
        logger.debug("Plugins directory not found: %s", plugins_path)
        return 0

    import sys

    # Add plugins dir to path if not already there
    plugins_str = str(plugins_path.resolve())
    if plugins_str not in sys.path:
        sys.path.insert(0, plugins_str)

    for item in plugins_path.iterdir():
        if item.name.startswith("_") or item.name.startswith("."):
            continue

        module_name = item.stem if item.is_file() and item.suffix == ".py" else None
        if item.is_dir() and (item / "__init__.py").exists():
            module_name = item.name

        if module_name is None:
            continue

        try:
            importlib.import_module(module_name)
            logger.debug("Imported plugin module: %s", module_name)
        except Exception as e:
            logger.warning("Failed to import plugin %s: %s", module_name, e)

    discovered = len(_registry) - before
    if discovered > 0:
        logger.info("Discovered %d new agent type(s) from plugins", discovered)
    return discovered

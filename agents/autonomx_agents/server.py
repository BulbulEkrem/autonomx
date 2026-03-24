"""AutoNomX Agent Runtime — gRPC server entry point."""

from __future__ import annotations

import asyncio
import logging
import os
import signal
from pathlib import Path

import grpc

from autonomx_agents.core.registry import discover_agents, discover_plugins, list_agents
from autonomx_agents.grpc_services.agent_servicer import AgentServicer
from autonomx_agents.llm import LLMGateway

logger = logging.getLogger(__name__)

GRPC_PORT = int(os.getenv("GRPC_PORT", "50051"))
MAX_WORKERS = int(os.getenv("GRPC_MAX_WORKERS", "10"))
LLM_CONFIG_PATH = os.getenv("LLM_CONFIG", "config/llm.yaml")


async def serve() -> None:
    """Start the gRPC server with all services registered."""

    # ── Discover agents ──────────────────────────────────────
    discovered = discover_agents()
    plugins = discover_plugins()
    logger.info(
        "Agent registry: %d built-in + %d plugin(s) = %s",
        discovered,
        plugins,
        list_agents(),
    )

    # ── Initialize LLM Gateway ───────────────────────────────
    gateway = LLMGateway.from_config(LLM_CONFIG_PATH)
    logger.info("LLM Gateway ready: providers=%s", gateway.list_providers())

    # ── Create gRPC server ───────────────────────────────────
    server = grpc.aio.server(
        options=[
            ("grpc.max_send_message_length", 50 * 1024 * 1024),  # 50MB
            ("grpc.max_receive_message_length", 50 * 1024 * 1024),
        ],
    )

    # Register services
    # NOTE: After `make proto`, uncomment the proper registration:
    # from autonomx_agents.grpc_services.generated import agent_service_pb2_grpc
    # agent_service_pb2_grpc.add_AgentServiceServicer_to_server(AgentServicer(), server)
    _servicer = AgentServicer()
    logger.info("AgentServicer created (proto stubs pending — run `make proto`)")

    # Add health check via gRPC reflection (useful for debugging)
    # from grpc_reflection.v1alpha import reflection
    # reflection.enable_server_reflection(..., server)

    listen_addr = f"[::]:{GRPC_PORT}"
    server.add_insecure_port(listen_addr)

    logger.info("Agent runtime starting on %s", listen_addr)
    await server.start()

    # ── Graceful shutdown on SIGTERM/SIGINT ───────────────────
    shutdown_event = asyncio.Event()

    def _signal_handler() -> None:
        logger.info("Shutdown signal received")
        shutdown_event.set()

    loop = asyncio.get_running_loop()
    for sig in (signal.SIGTERM, signal.SIGINT):
        loop.add_signal_handler(sig, _signal_handler)

    logger.info("Agent runtime ready. Waiting for requests...")
    await shutdown_event.wait()

    logger.info("Shutting down gracefully (5s grace period)...")
    await server.stop(grace=5)
    logger.info("Agent runtime stopped.")


def main() -> None:
    """Entry point."""
    logging.basicConfig(
        level=os.getenv("LOG_LEVEL", "INFO").upper(),
        format="%(asctime)s [%(levelname)s] %(name)s: %(message)s",
    )
    asyncio.run(serve())


if __name__ == "__main__":
    main()

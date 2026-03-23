"""AutoNomX Agent Runtime — gRPC server entry point."""

import asyncio
import logging
import os
from concurrent import futures

import grpc

logger = logging.getLogger(__name__)

GRPC_PORT = int(os.getenv("GRPC_PORT", "50051"))


async def serve() -> None:
    """Start the gRPC server."""
    server = grpc.aio.server()

    # TODO (M2): Register gRPC service implementations
    # agent_service_pb2_grpc.add_AgentServiceServicer_to_server(AgentServicer(), server)

    server.add_insecure_port(f"[::]:{GRPC_PORT}")
    logger.info("Agent runtime starting on port %d", GRPC_PORT)
    await server.start()
    await server.wait_for_termination()


def main() -> None:
    """Entry point."""
    logging.basicConfig(
        level=logging.INFO,
        format="%(asctime)s [%(levelname)s] %(name)s: %(message)s",
    )
    asyncio.run(serve())


if __name__ == "__main__":
    main()

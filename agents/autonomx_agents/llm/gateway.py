"""LLM Gateway — unified access to all LLM providers via LiteLLM."""

from __future__ import annotations

import logging
from dataclasses import dataclass
from typing import Any

import litellm

logger = logging.getLogger(__name__)


@dataclass
class LlmResponse:
    """Response from an LLM call."""

    content: str
    model: str
    prompt_tokens: int = 0
    completion_tokens: int = 0
    total_tokens: int = 0


async def completion(
    model: str,
    messages: list[dict[str, str]],
    temperature: float = 0.7,
    max_tokens: int = 4096,
    **kwargs: Any,
) -> LlmResponse:
    """Call an LLM via LiteLLM."""
    logger.info("LLM call: model=%s, messages=%d", model, len(messages))

    response = await litellm.acompletion(
        model=model,
        messages=messages,
        temperature=temperature,
        max_tokens=max_tokens,
        **kwargs,
    )

    usage = response.usage
    return LlmResponse(
        content=response.choices[0].message.content or "",
        model=response.model or model,
        prompt_tokens=usage.prompt_tokens if usage else 0,
        completion_tokens=usage.completion_tokens if usage else 0,
        total_tokens=usage.total_tokens if usage else 0,
    )

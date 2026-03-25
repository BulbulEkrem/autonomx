"""LLM Gateway — unified access to all LLM providers via LiteLLM.

Supports model prefix routing:
  - ollama/model-name     → Ollama local
  - lm_studio/model-name  → LM Studio local
  - openai/model-name     → OpenAI API
  - anthropic/model-name  → Anthropic API

LiteLLM handles the actual routing based on the model prefix.
"""

from __future__ import annotations

import json
import logging
import os
from pathlib import Path
from typing import Any, AsyncIterator

import litellm
import yaml
from pydantic import BaseModel, Field

logger = logging.getLogger(__name__)


class LlmResponse(BaseModel):
    """Response from an LLM call."""

    content: str
    model: str
    prompt_tokens: int = 0
    completion_tokens: int = 0
    total_tokens: int = 0
    raw_response: dict[str, Any] = Field(default_factory=dict)


class LlmStreamChunk(BaseModel):
    """A single chunk from a streaming LLM response."""

    content: str
    is_final: bool = False
    model: str = ""
    total_tokens: int = 0


class ProviderConfig(BaseModel):
    """Configuration for an LLM provider."""

    name: str
    type: str = "local"  # local | api
    base_url: str = ""
    api_key: str = ""
    api_key_env: str = ""
    priority: int = 1
    models: list[str] = Field(default_factory=list)


class LLMGateway:
    """Unified LLM gateway using LiteLLM for multi-provider support.

    Usage:
        gateway = LLMGateway.from_config("config/llm.yaml")
        response = await gateway.completion("ollama/qwen2.5-coder:14b", messages)
    """

    def __init__(self, providers: dict[str, ProviderConfig] | None = None) -> None:
        self.providers = providers or {}
        self._configure_litellm()

    @classmethod
    def from_config(cls, config_path: str | Path) -> LLMGateway:
        """Create gateway from llm.yaml config file."""
        path = Path(config_path)
        if not path.exists():
            logger.warning("LLM config not found: %s, using defaults", path)
            return cls()

        with open(path) as f:
            data = yaml.safe_load(f) or {}

        providers: dict[str, ProviderConfig] = {}
        for key, pdata in data.get("providers", {}).items():
            providers[key] = ProviderConfig(**pdata)

        return cls(providers=providers)

    def _configure_litellm(self) -> None:
        """Configure LiteLLM with provider settings."""
        # Suppress LiteLLM's verbose logging
        litellm.set_verbose = False

        for key, provider in self.providers.items():
            # Set API keys from environment if specified
            if provider.api_key_env:
                api_key = os.getenv(provider.api_key_env, "")
                if api_key:
                    provider.api_key = api_key

            # Configure base URLs for local providers
            if provider.type == "local" and provider.base_url:
                if key == "ollama":
                    os.environ.setdefault("OLLAMA_API_BASE", provider.base_url)
                elif key == "lm_studio":
                    os.environ.setdefault("LM_STUDIO_API_BASE", provider.base_url)

        logger.info(
            "LLM Gateway configured with %d provider(s): %s",
            len(self.providers),
            list(self.providers.keys()),
        )

    def _resolve_model_and_provider(
        self, model: str
    ) -> tuple[str, str, ProviderConfig | None]:
        """Resolve model name and provider for LiteLLM.

        For lm_studio provider, rewrites model to openai/ prefix since
        LM Studio exposes an OpenAI-compatible API.

        Returns:
            Tuple of (litellm_model_name, provider_name, provider_config)
        """
        provider_name = self._extract_provider(model)
        provider = self.providers.get(provider_name)

        litellm_model = model
        if provider_name == "lm_studio":
            # Strip lm_studio/ prefix and re-add as openai/ for LiteLLM
            actual_model = model.split("/", 1)[1] if "/" in model else model
            litellm_model = f"openai/{actual_model}"

        return litellm_model, provider_name, provider

    async def completion(
        self,
        model: str,
        messages: list[dict[str, str]],
        temperature: float = 0.7,
        max_tokens: int = 4096,
        response_format: dict[str, str] | None = None,
        **kwargs: Any,
    ) -> LlmResponse:
        """Call an LLM via LiteLLM.

        Args:
            model: Model identifier with provider prefix (e.g., "ollama/qwen2.5-coder:14b")
            messages: Chat messages in OpenAI format
            temperature: Sampling temperature
            max_tokens: Maximum tokens to generate
            response_format: Optional response format (e.g., {"type": "json_object"})
            **kwargs: Additional LiteLLM parameters
        """
        litellm_model, provider_name, provider = self._resolve_model_and_provider(model)

        # Inject API key if available
        call_kwargs: dict[str, Any] = {
            "model": litellm_model,
            "messages": messages,
            "temperature": temperature,
            "max_tokens": max_tokens,
            **kwargs,
        }

        if provider and provider.api_key:
            call_kwargs["api_key"] = provider.api_key
        if provider and provider.base_url and provider.type == "local":
            call_kwargs["api_base"] = provider.base_url
        if response_format:
            call_kwargs["response_format"] = response_format

        logger.info("LLM call: model=%s, messages=%d", model, len(messages))

        response = await litellm.acompletion(**call_kwargs)

        usage = response.usage
        return LlmResponse(
            content=response.choices[0].message.content or "",
            model=response.model or model,
            prompt_tokens=usage.prompt_tokens if usage else 0,
            completion_tokens=usage.completion_tokens if usage else 0,
            total_tokens=usage.total_tokens if usage else 0,
        )

    async def completion_json(
        self,
        model: str,
        messages: list[dict[str, str]],
        temperature: float = 0.3,
        max_tokens: int = 4096,
        **kwargs: Any,
    ) -> tuple[dict[str, Any], LlmResponse]:
        """Call LLM and parse response as JSON.

        Returns:
            Tuple of (parsed JSON dict, raw LlmResponse)
        """
        response = await self.completion(
            model=model,
            messages=messages,
            temperature=temperature,
            max_tokens=max_tokens,
            response_format={"type": "json_object"},
            **kwargs,
        )

        content = response.content.strip()
        # Handle markdown code blocks
        if content.startswith("```"):
            lines = content.split("\n")
            content = "\n".join(lines[1:-1]) if len(lines) > 2 else content

        parsed = json.loads(content)
        return parsed, response

    async def stream(
        self,
        model: str,
        messages: list[dict[str, str]],
        temperature: float = 0.7,
        max_tokens: int = 4096,
        **kwargs: Any,
    ) -> AsyncIterator[LlmStreamChunk]:
        """Stream LLM response chunks.

        Usage:
            async for chunk in gateway.stream(model, messages):
                print(chunk.content, end="")
        """
        litellm_model, provider_name, provider = self._resolve_model_and_provider(model)

        call_kwargs: dict[str, Any] = {
            "model": litellm_model,
            "messages": messages,
            "temperature": temperature,
            "max_tokens": max_tokens,
            "stream": True,
            **kwargs,
        }

        if provider and provider.api_key:
            call_kwargs["api_key"] = provider.api_key
        if provider and provider.base_url and provider.type == "local":
            call_kwargs["api_base"] = provider.base_url

        logger.info("LLM stream: model=%s, messages=%d", litellm_model, len(messages))

        response = await litellm.acompletion(**call_kwargs)

        async for chunk in response:
            delta = chunk.choices[0].delta
            content = delta.content or ""
            finish_reason = chunk.choices[0].finish_reason

            yield LlmStreamChunk(
                content=content,
                is_final=finish_reason is not None,
                model=chunk.model or model,
            )

    def get_provider(self, model: str) -> ProviderConfig | None:
        """Get the provider config for a model."""
        provider_name = self._extract_provider(model)
        return self.providers.get(provider_name)

    def list_providers(self) -> list[str]:
        """List configured provider names."""
        return list(self.providers.keys())

    @staticmethod
    def _extract_provider(model: str) -> str:
        """Extract provider name from model string (e.g., 'ollama/model' -> 'ollama')."""
        if "/" in model:
            return model.split("/", 1)[0]
        return "ollama"  # default provider

"""Tests for the LLM Gateway."""

from autonomx_agents.llm import LLMGateway, LlmResponse, LlmStreamChunk, ProviderConfig
from autonomx_agents.llm.prompts import list_prompts


class TestProviderConfig:
    def test_defaults(self):
        p = ProviderConfig(name="test")
        assert p.type == "local"
        assert p.priority == 1
        assert p.models == []

    def test_api_provider(self):
        p = ProviderConfig(
            name="openai",
            type="api",
            base_url="https://api.openai.com/v1",
            api_key_env="OPENAI_API_KEY",
            priority=10,
        )
        assert p.type == "api"
        assert p.priority == 10


class TestLlmResponse:
    def test_creation(self):
        r = LlmResponse(content="hello", model="ollama/test")
        assert r.content == "hello"
        assert r.total_tokens == 0

    def test_with_usage(self):
        r = LlmResponse(
            content="world",
            model="ollama/test",
            prompt_tokens=10,
            completion_tokens=5,
            total_tokens=15,
        )
        assert r.total_tokens == 15


class TestLlmStreamChunk:
    def test_chunk(self):
        c = LlmStreamChunk(content="hi")
        assert c.is_final is False

    def test_final_chunk(self):
        c = LlmStreamChunk(content="", is_final=True, model="test")
        assert c.is_final is True


class TestLLMGateway:
    def test_create_empty(self):
        gw = LLMGateway()
        assert gw.list_providers() == []

    def test_create_with_providers(self):
        gw = LLMGateway(providers={
            "ollama": ProviderConfig(name="Ollama", base_url="http://localhost:11434"),
            "openai": ProviderConfig(name="OpenAI", type="api"),
        })
        assert "ollama" in gw.list_providers()
        assert "openai" in gw.list_providers()

    def test_extract_provider(self):
        assert LLMGateway._extract_provider("ollama/qwen:14b") == "ollama"
        assert LLMGateway._extract_provider("openai/gpt-4o") == "openai"
        assert LLMGateway._extract_provider("anthropic/claude-sonnet") == "anthropic"
        assert LLMGateway._extract_provider("bare-model") == "ollama"

    def test_get_provider(self):
        gw = LLMGateway(providers={
            "ollama": ProviderConfig(name="Ollama", base_url="http://localhost:11434"),
        })
        assert gw.get_provider("ollama/qwen:14b") is not None
        assert gw.get_provider("openai/gpt-4o") is None

    def test_from_config_nonexistent(self):
        gw = LLMGateway.from_config("/nonexistent/path.yaml")
        assert gw.list_providers() == []

    def test_from_config_yaml(self, tmp_path):
        config_file = tmp_path / "llm.yaml"
        config_file.write_text("""
providers:
  ollama:
    name: "Ollama"
    type: "local"
    base_url: "http://localhost:11434"
    priority: 1
    models:
      - "qwen2.5-coder:14b"
  openai:
    name: "OpenAI"
    type: "api"
    base_url: "https://api.openai.com/v1"
    api_key_env: "OPENAI_API_KEY"
    priority: 10
""")
        gw = LLMGateway.from_config(config_file)
        assert len(gw.list_providers()) == 2
        assert gw.providers["ollama"].base_url == "http://localhost:11434"
        assert gw.providers["openai"].type == "api"


class TestPrompts:
    def test_list_prompts(self):
        prompts = list_prompts()
        assert isinstance(prompts, list)

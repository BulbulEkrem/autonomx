"""Prompt templates for agents."""

from __future__ import annotations

from pathlib import Path
from typing import Any

_PROMPTS_DIR = Path(__file__).parent


def load_prompt(name: str, **kwargs: Any) -> str:
    """Load a prompt template by name and format with kwargs.

    Looks for {name}.txt in the prompts directory.
    """
    prompt_file = _PROMPTS_DIR / f"{name}.txt"
    if not prompt_file.exists():
        raise FileNotFoundError(f"Prompt template not found: {name}")

    template = prompt_file.read_text()
    if kwargs:
        template = template.format(**kwargs)
    return template


def list_prompts() -> list[str]:
    """List available prompt template names."""
    return [f.stem for f in _PROMPTS_DIR.glob("*.txt")]

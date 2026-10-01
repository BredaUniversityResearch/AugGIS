"""Find the layers a request is about, out of every layer on EMODnet.

The same idea as /command, stretched to the whole catalogue: every layer goes in the prompt as
"id: name (theme)", and the answer is constrained to those ids by a JSON schema, so the model
cannot name a layer that does not exist. The schema decides which ids are legal. It does not
decide which are right, or how many the model lists.
"""

import json
import os

import httpx

LLM_URL = os.getenv("LLM_URL", "http://llm:11434")
LLM_MODEL = os.getenv("LLM_MODEL", "llama3.2:3b")
SEARCH_PROMPT_PATH = os.getenv("SEARCH_PROMPT_PATH", "/app/search-prompt.json")

MAX_PICKS = 10

# Ollama's default is 4096 tokens, and anything past it is dropped without an error. The 820
# layer lines are about 14,000 tokens, so the model would only see the end of the list.
CONTEXT_TOKENS = 20480


def load_search_prompt() -> dict:
    """Read search-prompt.json, on every request so an edit applies on a container restart.

    Returns:
        The parsed file, with "system", "examples" and "whisper_vocabulary" keys.
    """
    with open(SEARCH_PROMPT_PATH, encoding="utf-8") as fh:
        return json.load(fh)


def layer_text(layer: dict) -> str:
    """The text the model reads for a layer: its name and its theme."""
    return layer["spoken"] + " (" + layer["group"] + ")"


async def search(request: str, layers: list[dict]) -> dict:
    """Let the language model choose the layers a request is about, from all of them.

    Args:
        request: What the speaker said.
        layers: The catalogue, each with "id", "source", "group" and "spoken".

    Returns:
        {"matches": {"emodnet": [{"id": "E205"}, ...]}}, best first, each layer once.
    """
    prompt = load_search_prompt()
    ids = []
    lines = []
    for layer in layers:
        ids.append(layer["id"])
        lines.append(layer["id"] + ": " + layer_text(layer))

    # The list goes first and never changes, so Ollama reads it once and reuses it: the first
    # request takes about 20 seconds, later ones 1 to 2.
    messages = [{"role": "system", "content": prompt["system"] + "\n".join(lines)}]
    for example in prompt["examples"]:
        messages.append({"role": "user", "content": example["user"]})
        messages.append({"role": "assistant", "content": json.dumps(example["assistant"])})
    messages.append({"role": "user", "content": request})

    schema = {
        "type": "object",
        "properties": {
            "layers": {
                "type": "array",
                "maxItems": MAX_PICKS,
                "items": {"type": "string", "enum": ids},
            }
        },
        "required": ["layers"],
    }
    async with httpx.AsyncClient(timeout=180) as client:
        response = await client.post(
            f"{LLM_URL}/api/chat",
            json={
                "model": LLM_MODEL,
                "stream": False,
                "keep_alive": -1,
                "format": schema,
                "options": {"temperature": 0, "num_ctx": CONTEXT_TOKENS, "num_predict": 300},
                "messages": messages,
            },
        )
    response.raise_for_status()
    chosen = json.loads(response.json()["message"]["content"]).get("layers") or []

    # The grammar decides which ids are legal, not how often one appears. The model repeats.
    matches = []
    seen = []
    for layer_id in chosen:
        if layer_id not in seen:
            seen.append(layer_id)
            matches.append({"id": layer_id})
    return {"matches": {"emodnet": matches}}

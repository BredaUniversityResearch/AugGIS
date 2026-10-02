"""Find the layers a request is about, out of every layer on both map servers.

The language model cannot be handed all ~1300 layer names and asked to pick. Measured
on the real list: it fits (23,000 tokens, 7.5 of 8.2 GB of graphics memory) and the
answer is always valid, but it picked 0 to 3 right layers in 10 and never answered "nothing".
So the model only ever sees a short list:

    1. shortlist  the 12 layers per server whose words are closest, plus the 12 whose
                  meaning is closest, so at most 24 per server
    2. gate       if no layer is close in either way ("load the moon"), stop there
    3. pick       the model chooses from each server's shortlist, constrained to its ids

Word closeness catches exact names ("pipelines") and spelling ("bath" -> "bathymetry").
Meaning closeness catches different words for the same thing ("depth" -> "bathymetry",
"where do people fish" -> "fishing effort"), which no word comparison can.
"""

import json
import math
import os
import re

import httpx

LLM_URL = os.getenv("LLM_URL", "http://llm:11434")
LLM_MODEL = os.getenv("LLM_MODEL", "llama3.2:3b")
EMBED_MODEL = os.getenv("EMBED_MODEL", "nomic-embed-text")
SEARCH_PROMPT_PATH = os.getenv("SEARCH_PROMPT_PATH", "/app/search-prompt.json")

SOURCES = ["geoserver", "emodnet"]
SHORTLIST_SIZE = 12
MAX_PICKS = 6

# Below both of these, nothing in the catalogue is about the request. Set from the 12 test
# requests: real requests scored at least 0.60 in meaning, "load the moon"
# scored 0.505 in meaning and 0.10 in words. Twelve requests is a small sample.
CLOSE_IN_MEANING = 0.55
CLOSE_IN_WORDS = 0.30

# Without the model, a shortlisted layer is kept when its combined score is within this much
# of the best layer's. Words count half: a shared word piece is weaker evidence than meaning.
SCORE_GAP = 0.12
WORDS_WEIGHT = 0.5

# Words in a request that say what to do rather than which layer.
COMMAND_WORDS = {
    "load", "show", "me", "the", "a", "an", "of", "in", "on", "and", "layer", "layers",
    "map", "maps", "please", "turn", "display", "give", "hide", "where", "do", "want",
    "see", "all", "some",
}

# Meaning vectors already worked out, by text. A layer's vector never changes, so the first
# request pays for all ~1300 (about 11 seconds) and later requests only for new layers.
known_vectors: dict[str, list[float]] = {}


def load_search_prompt() -> dict:
    """Read search-prompt.json, on every request so an edit applies on a container restart.

    Returns:
        The parsed file, with "system", "examples" and "whisper_vocabulary" keys.
    """
    with open(SEARCH_PROMPT_PATH, encoding="utf-8") as fh:
        return json.load(fh)


def layer_text(layer: dict) -> str:
    """The text a layer is compared on: its name and its region or theme.

    The region is in it so "wind farms in the baltic" can prefer the Baltic layers.
    """
    return layer["spoken"] + " (" + layer["group"] + ")"


def words(text: str) -> list[str]:
    """Split text into lowercase words and numbers."""
    return re.findall(r"[a-z0-9]+", text.lower())


def word_features(text: str) -> dict[str, float]:
    """Count the words of a text and the 3-letter pieces of each word.

    The pieces are what let "bath" score against "bathymetry", which share "bat" and
    "ath" but are different words. A whole word counts twice as much as one piece.

    Args:
        text: A request or a layer's text.

    Returns:
        A count per feature, such as {"w:wind": 1, "c:<wi": 0.5, "c:win": 0.5, ...}.
    """
    features: dict[str, float] = {}
    for word in words(text):
        if word in COMMAND_WORDS:
            continue
        key = "w:" + word
        features[key] = features.get(key, 0) + 1

        padded = "<" + word + ">"
        for start in range(len(padded) - 2):
            key = "c:" + padded[start:start + 3]
            features[key] = features.get(key, 0) + 0.5
    return features


def to_unit_vector(features: dict[str, float], rarity: dict[str, float]) -> dict[str, float]:
    """Weigh each feature by how rare it is, then scale the whole to length 1.

    A feature in every layer ("areas") says little; one in a few layers ("pipelines") says
    a lot. Scaling to length 1 means a long layer name does not win by being long.
    """
    weighted = {}
    for key, count in features.items():
        weighted[key] = count * rarity.get(key, 0)

    length = math.sqrt(sum(value * value for value in weighted.values()))
    if length == 0:
        return {}

    unit = {}
    for key, value in weighted.items():
        unit[key] = value / length
    return unit


def word_scores(request: str, layers: list[dict]) -> dict[str, float]:
    """Score every layer by the words and word pieces it shares with the request.

    Args:
        request: What the speaker said.
        layers: The catalogue.

    Returns:
        A score per layer id, 0 for nothing shared up to 1 for the same words.
    """
    layer_features = {}
    layers_with = {}
    for layer in layers:
        features = word_features(layer_text(layer))
        layer_features[layer["id"]] = features
        for key in features:
            layers_with[key] = layers_with.get(key, 0) + 1

    rarity = {}
    for key, count in layers_with.items():
        rarity[key] = math.log((len(layers) + 1) / (count + 1))

    request_vector = to_unit_vector(word_features(request), rarity)
    scores = {}
    for layer_id, features in layer_features.items():
        layer_vector = to_unit_vector(features, rarity)
        score = 0.0
        for key, value in request_vector.items():
            score += value * layer_vector.get(key, 0)
        scores[layer_id] = score
    return scores


def normalise(vector: list[float]) -> list[float]:
    """Scale a vector to length 1, so comparing two is one multiply-and-add."""
    length = math.sqrt(sum(value * value for value in vector))
    return [value / length for value in vector]


async def embed(client: httpx.AsyncClient, texts: list[str]) -> list[list[float]]:
    """Ask the embedding model for one meaning vector per text, in batches of 128.

    Args:
        client: An open HTTP client.
        texts: The texts, each starting with nomic-embed-text's "search_query: " or
            "search_document: " prefix, which this model was trained to expect.

    Returns:
        One vector of 768 numbers per text, scaled to length 1.
    """
    vectors = []
    for start in range(0, len(texts), 128):
        response = await client.post(
            f"{LLM_URL}/api/embed",
            json={"model": EMBED_MODEL, "input": texts[start:start + 128], "keep_alive": -1},
        )
        response.raise_for_status()
        for vector in response.json()["embeddings"]:
            vectors.append(normalise(vector))
    return vectors


async def meaning_scores(client: httpx.AsyncClient, request: str, layers: list[dict]) -> dict[str, float]:
    """Score every layer by how close its meaning is to the request's.

    Args:
        client: An open HTTP client.
        request: What the speaker said.
        layers: The catalogue.

    Returns:
        A score per layer id. In practice 0.45 for unrelated up to about 0.85.
    """
    missing = []
    for layer in layers:
        text = "search_document: " + layer_text(layer)
        if text not in known_vectors and text not in missing:
            missing.append(text)
    if missing:
        for text, vector in zip(missing, await embed(client, missing)):
            known_vectors[text] = vector

    request_vector = (await embed(client, ["search_query: " + request]))[0]
    scores = {}
    for layer in layers:
        layer_vector = known_vectors["search_document: " + layer_text(layer)]
        scores[layer["id"]] = sum(a * b for a, b in zip(request_vector, layer_vector))
    return scores


def best_ids(scores: dict[str, float], allowed: set[str], count: int) -> list[str]:
    """The ids with the highest scores, out of the allowed ones."""
    ranked = sorted(allowed, key=scores.get, reverse=True)
    return ranked[:count]


def shortlist(layers: list[dict], source: str, by_words: dict, by_meaning: dict) -> list[str]:
    """The layers of one server that the model gets to choose from.

    Args:
        layers: The catalogue.
        source: "geoserver" or "emodnet".
        by_words: Word scores per id.
        by_meaning: Meaning scores per id.

    Returns:
        Up to 24 ids: the 12 best by words, then the 12 best by meaning not already in.
    """
    allowed = set()
    for layer in layers:
        if layer["source"] == source:
            allowed.add(layer["id"])

    candidates = best_ids(by_words, allowed, SHORTLIST_SIZE)
    for layer_id in best_ids(by_meaning, allowed, SHORTLIST_SIZE):
        if layer_id not in candidates:
            candidates.append(layer_id)
    return candidates


async def pick(client: httpx.AsyncClient, request: str, candidates: list[str], by_id: dict) -> list[str]:
    """Let the language model choose the layers the request is about.

    The answer is constrained to the candidates' ids, so the model cannot name a layer that
    does not exist or was not shortlisted. It does not stop it listing too many: the 3B model
    tends to fill all 6 places, which is why the caller shows each pick's meaning score.

    Args:
        client: An open HTTP client.
        request: What the speaker said.
        candidates: Ids from shortlist().
        by_id: The catalogue by id.

    Returns:
        The chosen ids, best first, each once.
    """
    prompt = load_search_prompt()
    lines = []
    for layer_id in candidates:
        lines.append(layer_id + ": " + layer_text(by_id[layer_id]))
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
                "items": {"type": "string", "enum": candidates},
            }
        },
        "required": ["layers"],
    }
    response = await client.post(
        f"{LLM_URL}/api/chat",
        json={
            "model": LLM_MODEL,
            "stream": False,
            "keep_alive": -1,
            "format": schema,
            "options": {"temperature": 0, "num_ctx": 4096, "num_predict": 300},
            "messages": messages,
        },
    )
    response.raise_for_status()
    chosen = json.loads(response.json()["message"]["content"]).get("layers") or []

    # The grammar decides which ids are legal, not how often one appears. The model repeats.
    unique = []
    for layer_id in chosen:
        if layer_id not in unique:
            unique.append(layer_id)
    return unique


def pick_by_scores(candidates: list[str], combined: dict[str, float], best: float) -> list[str]:
    """Choose layers without the model: the shortlisted ones scoring close to the best.

    Args:
        candidates: Ids from shortlist().
        combined: Meaning score plus half the word score, per id.
        best: The highest combined score of any layer on either server.

    Returns:
        Up to MAX_PICKS ids, best first.
    """
    ranked = sorted(candidates, key=combined.get, reverse=True)
    chosen = []
    for layer_id in ranked:
        if combined[layer_id] >= best - SCORE_GAP and len(chosen) < MAX_PICKS:
            chosen.append(layer_id)
    return chosen


async def search(request: str, layers: list[dict], use_model: bool = True) -> dict:
    """Find the layers on each server that a request is about.

    Args:
        request: What the speaker said.
        layers: The catalogue, each with "id", "source", "group" and "spoken".
        use_model: False skips the language model and keeps the layers whose scores are
            close to the best, to compare the two.

    Returns:
        {"nothing_close": bool,
         "matches": {"geoserver": [{"id": "G129", "meaning": 0.81, "words": 0.92}, ...],
                     "emodnet": [...]},
         "shortlists": {"geoserver": [ids], "emodnet": [ids]}}
    """
    by_id = {}
    for layer in layers:
        by_id[layer["id"]] = layer

    async with httpx.AsyncClient(timeout=180) as client:
        by_words = word_scores(request, layers)
        by_meaning = await meaning_scores(client, request, layers)

        best_meaning = max(by_meaning.values())
        best_words = max(by_words.values())
        nothing_close = best_meaning < CLOSE_IN_MEANING and best_words < CLOSE_IN_WORDS

        combined = {}
        for layer_id, meaning in by_meaning.items():
            combined[layer_id] = meaning + WORDS_WEIGHT * by_words[layer_id]
        best_combined = max(combined.values())

        matches = {}
        shortlists = {}
        for source in SOURCES:
            matches[source] = []
            shortlists[source] = shortlist(layers, source, by_words, by_meaning)
            if nothing_close or not shortlists[source]:
                continue

            if use_model:
                chosen = await pick(client, request, shortlists[source], by_id)
            else:
                chosen = pick_by_scores(shortlists[source], combined, best_combined)

            for layer_id in chosen:
                matches[source].append({
                    "id": layer_id,
                    "meaning": round(by_meaning[layer_id], 3),
                    "words": round(by_words[layer_id], 3),
                })

    return {"nothing_close": nothing_close, "matches": matches, "shortlists": shortlists}

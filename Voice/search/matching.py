"""Score every layer against a few search words, by shared words and by meaning.

Word closeness catches exact names ("pipelines") and spelling ("bath" -> "bathymetry").
Meaning closeness catches different words for the same thing ("depth" -> "bathymetry",
"where do people fish" -> "fishing effort"), which no word comparison can.

The meaning scores come from an embedding model (nomic-embed-text, run by Ollama), which
turns a text into 768 numbers so that texts about the same thing get similar numbers.
"""

import math
import os
import re

import httpx

EMBED_URL = os.getenv("EMBED_URL", "http://llm:11434")
EMBED_MODEL = os.getenv("EMBED_MODEL", "nomic-embed-text")

# Below both of these, nothing in the catalogue is about the request. Set from 12 test
# requests: real ones scored at least 0.60 in meaning, "load the moon" scored 0.505 in
# meaning and 0.10 in words. Twelve requests is a small sample.
CLOSE_IN_MEANING = 0.55
CLOSE_IN_WORDS = 0.30

# Words count half: a shared word piece is weaker evidence than a shared meaning.
WORDS_WEIGHT = 0.5

# Words in a request that say what to do rather than which layer.
COMMAND_WORDS = {
    "load", "show", "me", "the", "a", "an", "of", "in", "on", "and", "layer", "layers",
    "map", "maps", "please", "turn", "display", "give", "hide", "where", "do", "want",
    "see", "all", "some",
}


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
            f"{EMBED_URL}/api/embed",
            json={"model": EMBED_MODEL, "input": texts[start:start + 128], "keep_alive": -1},
        )
        response.raise_for_status()
        for vector in response.json()["embeddings"]:
            vectors.append(normalise(vector))
    return vectors


class Index:
    """The catalogue prepared for searching: each layer's word vector and meaning vector.

    Built once when the service starts, because a layer's vectors never change. A search
    then only has to work out the vectors of the few search words.
    """

    def __init__(self, layers: list[dict]):
        self.layers = layers
        self.by_id: dict[str, dict] = {}
        for layer in layers:
            self.by_id[layer["id"]] = layer
        self.rarity: dict[str, float] = {}
        self.word_vectors: dict[str, dict[str, float]] = {}
        self.meaning_vectors: dict[str, list[float]] = {}
        self.prepare_words()

    def prepare_words(self) -> None:
        """Work out every layer's word vector."""
        layer_features = {}
        layers_with: dict[str, int] = {}
        for layer in self.layers:
            features = word_features(layer_text(layer))
            layer_features[layer["id"]] = features
            for key in features:
                layers_with[key] = layers_with.get(key, 0) + 1

        for key, count in layers_with.items():
            self.rarity[key] = math.log((len(self.layers) + 1) / (count + 1))
        for layer_id, features in layer_features.items():
            self.word_vectors[layer_id] = to_unit_vector(features, self.rarity)

    async def prepare_meanings(self, client: httpx.AsyncClient) -> None:
        """Ask the embedding model for every layer's meaning vector, about 11 s for 1300."""
        texts = []
        for layer in self.layers:
            texts.append("search_document: " + layer_text(layer))
        vectors = await embed(client, texts)
        for layer, vector in zip(self.layers, vectors):
            self.meaning_vectors[layer["id"]] = vector

    def word_scores(self, query: str) -> dict[str, float]:
        """Score every layer by the words and word pieces it shares with the query.

        Returns:
            A score per layer id, 0 for nothing shared up to 1 for the same words.
        """
        query_vector = to_unit_vector(word_features(query), self.rarity)
        scores = {}
        for layer_id, layer_vector in self.word_vectors.items():
            score = 0.0
            for key, value in query_vector.items():
                score += value * layer_vector.get(key, 0)
            scores[layer_id] = score
        return scores

    async def meaning_scores(self, client: httpx.AsyncClient, query: str) -> dict[str, float]:
        """Score every layer by how close its meaning is to the query's.

        Returns:
            A score per layer id. In practice 0.45 for unrelated up to about 0.85.
        """
        query_vector = (await embed(client, ["search_query: " + query]))[0]
        scores = {}
        for layer_id, layer_vector in self.meaning_vectors.items():
            scores[layer_id] = sum(a * b for a, b in zip(query_vector, layer_vector))
        return scores

    async def search(self, client: httpx.AsyncClient, query: str, per_server: int) -> dict:
        """Find the layers on each server closest to the query.

        Args:
            client: An open HTTP client.
            query: A few search words.
            per_server: How many layers to return for each server.

        Returns:
            {"nothing_close": bool,
             "results": {"geoserver": [layer, ...], "emodnet": [layer, ...]}}, best first,
            where each layer is its catalogue entry plus its "meaning" and "words" scores.
        """
        by_words = self.word_scores(query)
        by_meaning = await self.meaning_scores(client, query)

        close_in_meaning = max(by_meaning.values()) >= CLOSE_IN_MEANING
        close_in_words = max(by_words.values()) >= CLOSE_IN_WORDS
        nothing_close = not close_in_meaning and not close_in_words

        combined = {}
        for layer_id, meaning in by_meaning.items():
            combined[layer_id] = meaning + WORDS_WEIGHT * by_words[layer_id]

        results: dict[str, list[dict]] = {"geoserver": [], "emodnet": []}
        if nothing_close:
            return {"nothing_close": True, "results": results}

        for layer_id in sorted(combined, key=combined.get, reverse=True):
            layer = self.by_id[layer_id]
            on_server = results[layer["source"]]
            if len(on_server) >= per_server:
                continue
            found = dict(layer)
            found["meaning"] = round(by_meaning[layer_id], 3)
            found["words"] = round(by_words[layer_id], 3)
            on_server.append(found)
        return {"nothing_close": False, "results": results}

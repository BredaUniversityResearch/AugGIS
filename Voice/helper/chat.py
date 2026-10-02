"""Find layers by talking: the conversation is kept, so a follow-up can change the search.

Each time the person says something, the model works in steps. Every step it writes one JSON
action:

    {"action": "search", "query": "bathymetry", "layers": [], "reply": ""}     the code searches
    {"action": "answer", "query": "", "layers": ["G64"], "reply": "I found..."}  the code stops

The answer's "reply" is what the model says back to the person, in conversation.

After a search the code adds the results to the conversation and asks for the next step. The
caller keeps the conversation and sends it back with the next sentence, so "no, search for
bathymetry instead" continues from what was already found instead of starting over.

Two models can play the model's part, chosen with CHAT_BACKEND:

    buas    the BUas AI Assistants API (the default). It keeps the conversation itself under a
            chat_key and answers in plain text, so the JSON is asked for in words and checked
            here. Measured: it kept to the format and wrote "bathymetry" for
            "bath layers" on its own.
    ollama  llama3.2:3b in the voice stack. The JSON is forced by a schema, so it cannot break,
            but the model never rewrites a search on its own.

Speech-to-text and the word and meaning search stay local either way.
"""

import json
import os
import re

import httpx

import search

CHAT_PROMPT_PATH = os.getenv("CHAT_PROMPT_PATH", "/app/chat-prompt.json")
CHAT_BACKEND = os.getenv("CHAT_BACKEND", "buas")
BUAS_URL = os.getenv("BUAS_URL", "https://ai-assistants.buas.nl/aioda-api/actions")
BUAS_USER_KEY = os.getenv("BUAS_USER_KEY", "")
BUAS_ASSISTANT_KEY = os.getenv("BUAS_ASSISTANT_KEY", "")

RESULTS_PER_SERVER = 10
MAX_ANSWER = 10
MAX_SEARCHES_PER_TURN = 3

# The lines this module writes into a search result message start with the layer id.
RESULT_LINE = re.compile(r"^([GE]\d+): ", re.MULTILINE)
RESULTS_HEADER = "Search results for "
NEXT_STEP = "\n\nReply with the next step as one line of JSON."
JSON_OBJECT = re.compile(r"\{.*\}", re.DOTALL)


def load_chat_prompt() -> dict:
    """Read chat-prompt.json, on every request so an edit applies on a container restart."""
    with open(CHAT_PROMPT_PATH, encoding="utf-8") as fh:
        return json.load(fh)


def found_ids(messages: list[dict]) -> list[str]:
    """Every layer id that any search in the conversation has returned, in order, once each.

    These are the only ids an answer may contain, so a follow-up such as "only the Baltic
    ones" can pick from results found several sentences ago.
    """
    ids = []
    for message in messages:
        if not message["content"].startswith(RESULTS_HEADER):
            continue
        for layer_id in RESULT_LINE.findall(message["content"]):
            if layer_id not in ids:
                ids.append(layer_id)
    return ids


def step_schema(allowed_ids: list[str]) -> dict:
    """The JSON shape every step must have, for the Ollama backend's grammar.

    Args:
        allowed_ids: Ids the answer may contain. Before the first search there are none, and
            an empty enum is a grammar Ollama cannot build, so a placeholder stands in.
    """
    if allowed_ids:
        id_values = allowed_ids
    else:
        id_values = ["none"]

    return {
        "type": "object",
        "properties": {
            "action": {"type": "string", "enum": ["search", "answer"]},
            "query": {"type": "string"},
            "layers": {
                "type": "array",
                "maxItems": MAX_ANSWER,
                "items": {"type": "string", "enum": id_values},
            },
            "reply": {"type": "string"},
        },
        "required": ["action", "query", "layers", "reply"],
    }


def parse_step(reply: str) -> dict | None:
    """Read a step out of the model's reply, or None if it is not one.

    The BUas model is only asked for the JSON, not forced, so this is where a reply that
    wandered off the format gets caught. The JSON is taken from the first "{" to the last "}",
    which also copes with a reply wrapped in a code block.
    """
    match = JSON_OBJECT.search(reply)
    if match is None:
        return None
    try:
        step = json.loads(match.group(0))
    except ValueError:
        return None

    if not isinstance(step, dict):
        return None
    if step.get("action") not in ("search", "answer"):
        return None

    query = step.get("query")
    if not isinstance(query, str):
        query = ""
    layers = step.get("layers")
    if not isinstance(layers, list):
        layers = []
    reply = step.get("reply")
    if not isinstance(reply, str):
        reply = ""

    return {"action": step["action"], "query": query, "layers": layers, "reply": reply}


async def run_search(client: httpx.AsyncClient, query: str, layers: list[dict], by_id: dict) -> dict:
    """Search the catalogue for the model's query: the 10 best per server by words and meaning.

    Args:
        client: An open HTTP client.
        query: The search words the model wrote.
        layers: The catalogue.
        by_id: The catalogue by id.

    Returns:
        {"query", "nothing_close", "found": {"geoserver": [ids], "emodnet": [ids]},
         "scores": {id: {"meaning", "words"}}, "text"}, where "text" is the message added to
        the conversation and "scores" lets the caller show why each layer was found.
    """
    by_words = search.word_scores(query, layers)
    by_meaning = await search.meaning_scores(client, query, layers)

    best_meaning = max(by_meaning.values())
    best_words = max(by_words.values())
    nothing_close = best_meaning < search.CLOSE_IN_MEANING and best_words < search.CLOSE_IN_WORDS

    found = {}
    scores = {}
    lines = []
    for source in search.SOURCES:
        found[source] = []
        if nothing_close:
            continue

        on_server = []
        for layer in layers:
            if layer["source"] == source:
                on_server.append(layer["id"])

        combined = {}
        for layer_id in on_server:
            combined[layer_id] = by_meaning[layer_id] + search.WORDS_WEIGHT * by_words[layer_id]

        ranked = sorted(on_server, key=combined.get, reverse=True)
        for layer_id in ranked[:RESULTS_PER_SERVER]:
            found[source].append(layer_id)
            scores[layer_id] = {
                "meaning": round(by_meaning[layer_id], 3),
                "words": round(by_words[layer_id], 3),
            }
            lines.append(layer_id + ": " + search.layer_text(by_id[layer_id]))

    if nothing_close:
        text = RESULTS_HEADER + json.dumps(query) + ":\nNothing in the catalogue is close to that."
    else:
        text = RESULTS_HEADER + json.dumps(query) + ":\n" + "\n".join(lines)

    return {"query": query, "nothing_close": nothing_close, "found": found, "scores": scores, "text": text}


async def ollama_step(client: httpx.AsyncClient, messages: list[dict]) -> str:
    """Ask llama3.2 for its next step, with the whole conversation, forced to the step schema."""
    prompt = load_chat_prompt()
    full = [{"role": "system", "content": prompt["system"]}] + messages
    response = await client.post(
        f"{search.LLM_URL}/api/chat",
        json={
            "model": search.LLM_MODEL,
            "stream": False,
            "keep_alive": -1,
            "format": step_schema(found_ids(messages)),
            # Each search adds about 20 lines, so a long conversation needs more than 4096.
            "options": {"temperature": 0, "num_ctx": 8192, "num_predict": 300},
            "messages": full,
        },
    )
    response.raise_for_status()
    return response.json()["message"]["content"]


async def buas_start(client: httpx.AsyncClient) -> str:
    """Open a new BUas conversation and return its chat_key."""
    response = await client.post(
        f"{BUAS_URL}/init_assistant",
        json={"user_key": BUAS_USER_KEY, "assistant_key": BUAS_ASSISTANT_KEY},
    )
    response.raise_for_status()
    # Wrong keys still answer 200, with {"error": "User not found", "status": "Failed"}.
    body = response.json()
    if "chat_key" not in body:
        raise RuntimeError("BUas refused to start a conversation (" + str(body.get("error")) + "). "
                           "Check BUAS_USER_KEY and BUAS_ASSISTANT_KEY in Voice/.env.")
    return body["chat_key"]


async def buas_step(client: httpx.AsyncClient, chat_key: str, message: str) -> str:
    """Send one message in a BUas conversation and return the reply.

    Only the new message is sent: the service keeps the conversation under the chat_key. An
    unknown chat_key does not fail, it silently starts a fresh conversation (tested
    2026-09-14), so the caller must keep the key it was given.
    """
    response = await client.post(
        f"{BUAS_URL}/ask_assistant",
        json={
            "user_key": BUAS_USER_KEY,
            "assistant_key": BUAS_ASSISTANT_KEY,
            "chat_key": chat_key,
            "message": message,
        },
    )
    response.raise_for_status()
    # The reply is plain text, not JSON.
    return response.text


async def chat(sentence: str, conversation: dict, layers: list[dict]) -> dict:
    """Continue the conversation with one new sentence from the person.

    Args:
        sentence: What the person just said.
        conversation: As returned by the previous call, or {} to start a new one.
        layers: The catalogue, each with "id", "source", "group" and "spoken".

    Returns:
        {"conversation": pass back with the next sentence,
         "searches": [{"query", "nothing_close", "found"}, ...] run during this turn,
         "picks": {"geoserver": [ids], "emodnet": [ids]} from the model's answer,
         "reply": what the model said back to the person,
         "problems": replies that were not a step, empty when all went well,
         "backend": "buas" or "ollama"}
    """
    by_id = {}
    for layer in layers:
        by_id[layer["id"]] = layer

    messages = list(conversation.get("messages", []))
    chat_key = conversation.get("chat_key")
    searches = []
    problems = []
    answer = []
    said = ""

    async with httpx.AsyncClient(timeout=180) as client:
        outgoing = sentence
        if CHAT_BACKEND == "buas" and not chat_key:
            chat_key = await buas_start(client)
            outgoing = load_chat_prompt()["buas_instructions"] + sentence
        messages.append({"role": "user", "content": sentence})

        # One more round than searches allowed, so after the last search the model still answers.
        for _ in range(MAX_SEARCHES_PER_TURN + 1):
            if CHAT_BACKEND == "buas":
                reply = await buas_step(client, chat_key, outgoing)
                step = parse_step(reply)
                if step is None:
                    problems.append(reply[:300])
                    reply = await buas_step(client, chat_key, "Reply with only the one line of JSON.")
                    step = parse_step(reply)
            else:
                reply = await ollama_step(client, messages)
                step = parse_step(reply)

            messages.append({"role": "assistant", "content": reply})
            if step is None:
                problems.append(reply[:300])
                break

            asked_to_search = step["action"] == "search"
            searches_left = len(searches) < MAX_SEARCHES_PER_TURN
            already_searched = False
            for done in searches:
                if done["query"] == step["query"]:
                    already_searched = True

            if asked_to_search and searches_left and not already_searched:
                result = await run_search(client, step["query"], layers, by_id)
                messages.append({"role": "user", "content": result["text"]})
                outgoing = result["text"] + NEXT_STEP
                searches.append({
                    "query": result["query"],
                    "nothing_close": result["nothing_close"],
                    "found": result["found"],
                    "scores": result["scores"],
                })
                continue

            answer = step["layers"]
            said = step["reply"]
            break

    # Only ids a search actually returned count. The BUas model is not held to them by a
    # grammar, so an id it made up is dropped here.
    allowed = found_ids(messages)
    picks = {}
    for source in search.SOURCES:
        picks[source] = []
    for layer_id in answer:
        if layer_id not in allowed:
            continue
        source = by_id[layer_id]["source"]
        if layer_id not in picks[source]:
            picks[source].append(layer_id)

    return {
        "conversation": {"messages": messages, "chat_key": chat_key},
        "searches": searches,
        "picks": picks,
        "reply": said,
        "problems": problems,
        "backend": CHAT_BACKEND,
    }

"""One spoken sentence in a person's conversation with the language model.

The model decides what the sentence asks for. It works in steps, each one line of JSON:

    {"action": "search", "query": "bathymetry"}                 search the layer catalogue
    {"action": "answer", "operations": [...], "layers": [...], "reply": "..."}   finished

After a search the results are sent back to the model and it gives its next step. An
answer can change layers on the map ("operations"), list catalogue layers it found
("layers"), or both, and always says something back ("reply").

The language model is the BUas AI Assistants API. It keeps each conversation itself, under a
chat_key, so only the new sentence is sent. The caller holds a small ticket and hands it back
with its next sentence:

    {"chat_key": "...", "found": [{"id": "G12", "meaning": 0.84}, ...], "version": "e9c62ca3ba54"}

"found" is every catalogue layer a search in this conversation returned, with how close its
meaning was to the search words. A follow-up such as "only the Baltic ones" may pick from
those without searching again. This service keeps
nothing between sentences, so each caller has its own conversation and a restart loses none.
"""

import json
import os

import httpx

import checks

VOICE_PROMPT_PATH = os.getenv("VOICE_PROMPT_PATH", "/app/voice-prompt.json")
SEARCH_URL = os.getenv("SEARCH_URL", "http://search:8090")
BUAS_URL = os.getenv("BUAS_URL", "https://ai-assistants.buas.nl/aioda-api/actions")
BUAS_USER_KEY = os.getenv("BUAS_USER_KEY", "")
BUAS_ASSISTANT_KEY = os.getenv("BUAS_ASSISTANT_KEY", "")

RESULTS_PER_SERVER = 10
MAX_SEARCHES_PER_TURN = 3
# Steps the model may take in one turn: 3 searches, a reminder or two, and the answer. Without a
# limit, a model that keeps asking for the same search would be asked again forever.
MAX_STEPS_PER_TURN = 6

ONLY_JSON = "Reply with only the one line of JSON."
NEXT_STEP = "\n\nReply with your next step as one line of JSON."
ALREADY_SEARCHED = "You already have the results for that search. Give your answer now, as one line of JSON."
NO_MORE_SEARCHES = "You have used all the searches for this turn. Give your answer now, as one line of JSON."


def load_voice_prompt() -> dict:
    """Read voice-prompt.json, on every request so an edit applies on a container restart."""
    with open(VOICE_PROMPT_PATH, encoding="utf-8") as fh:
        return json.load(fh)


async def start_conversation(client: httpx.AsyncClient) -> str:
    """Open a new BUas conversation and return its chat_key."""
    response = await client.post(
        f"{BUAS_URL}/init_assistant",
        json={"user_key": BUAS_USER_KEY, "assistant_key": BUAS_ASSISTANT_KEY},
    )
    response.raise_for_status()
    # Wrong keys still answer 200, with {"error": "User not found", "status": "Failed"}.
    body = response.json()
    if "chat_key" not in body:
        raise RuntimeError("the language model refused to start a conversation (" + str(body.get("error"))
                           + "). Check BUAS_USER_KEY and BUAS_ASSISTANT_KEY in Voice/.env.")
    return body["chat_key"]


async def ask(client: httpx.AsyncClient, chat_key: str, message: str) -> str:
    """Send one message in a BUas conversation and return the reply, which is plain text."""
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
    return response.text


async def ask_for_step(client: httpx.AsyncClient, chat_key: str, message: str, problems: list[str]) -> dict | None:
    """Send a message and read the model's step, asking once more if the reply is not one.

    Args:
        client: An open HTTP client.
        chat_key: The conversation.
        message: What to send.
        problems: Replies that were not a step are added here, for the caller to see.

    Returns:
        The step from checks.parse_step, or None if both replies were not a step.
    """
    reply = await ask(client, chat_key, message)
    step = checks.parse_step(reply)
    if step is not None:
        return step

    problems.append(reply[:300])
    reply = await ask(client, chat_key, ONLY_JSON)
    step = checks.parse_step(reply)
    if step is None:
        problems.append(reply[:300])
    return step


async def search_catalogue(client: httpx.AsyncClient, query: str) -> dict:
    """Ask the search service for the layers closest to the model's search words."""
    response = await client.post(
        f"{SEARCH_URL}/search",
        json={"query": query, "per_server": RESULTS_PER_SERVER},
    )
    response.raise_for_status()
    return response.json()


def results_message(found: dict) -> str:
    """Write a search's results as the message the model gets back, one layer per line."""
    header = "Search results for " + json.dumps(found["query"]) + ":\n"
    if found["nothing_close"]:
        return header + "Nothing in the catalogue is close to that."

    lines = []
    for source in ("geoserver", "emodnet"):
        for layer in found["results"][source]:
            lines.append(layer["id"] + ": " + layer["spoken"] + " (" + layer["group"] + "), closeness "
                         + format(layer["meaning"], ".2f"))
    return header + "\n".join(lines)


def closeness(layer: dict) -> float:
    """A picked layer's meaning score, for sorting the closest first."""
    return layer["meaning"]


async def picked_layers(client: httpx.AsyncClient, ids: list[str], scores: dict, version: str) -> dict:
    """Look the picked ids up in the catalogue and sort them by the server they are on.

    Args:
        client: An open HTTP client.
        ids: The picked ids.
        scores: The meaning score of every layer found in this conversation, by id.
        version: The catalogue version the ids were found in.

    Returns:
        {"geoserver": [{"id", "spoken", "group", "name", "meaning"}, ...], "emodnet": [...]},
        closest first. Both lists are empty when the catalogue has changed since, because the
        ids then mean other layers.
    """
    picks: dict[str, list[dict]] = {"geoserver": [], "emodnet": []}
    if not ids:
        return picks

    response = await client.post(f"{SEARCH_URL}/layers", json={"ids": ids})
    response.raise_for_status()
    looked_up = response.json()
    if looked_up["version"] != version:
        return picks

    for layer in looked_up["layers"]:
        picks[layer["source"]].append({
            "id": layer["id"],
            "spoken": layer["spoken"],
            "group": layer["group"],
            "name": layer["name"],
            "meaning": scores[layer["id"]],
        })
    for source in picks:
        picks[source].sort(key=closeness, reverse=True)
    return picks


async def turn(sentence: str, map_layers: list[str], ticket: dict) -> dict:
    """Continue a person's conversation with one new sentence.

    Args:
        sentence: What the person just said.
        map_layers: The names of the layers in the person's session, which may be shown or hidden.
        ticket: The "conversation" from the previous answer, or {} to start a new one.

    Returns:
        {"operations": [{"action": "hide", "layers": [...]}, ...] to apply to the map,
         "picks": {"geoserver": [...], "emodnet": [...]} catalogue layers found for the person,
         "reply": what the model says back,
         "searches": [{"query", "nothing_close"}, ...] run during this turn,
         "conversation": the ticket to send back with the next sentence,
         "problems": what went wrong on the way, such as a reply that was not a step. It can
             have entries although the turn worked, when a second try succeeded}

    Raises:
        httpx.HTTPError: If the language model or the search service did not answer.
        RuntimeError: If the language model refused the keys.
    """
    prompt = load_voice_prompt()

    # The ticket comes back from the caller, so its fields are checked and not trusted.
    chat_key = ticket.get("chat_key")
    if not isinstance(chat_key, str):
        chat_key = ""
    version = ticket.get("version")
    if not isinstance(version, str):
        version = ""
    found_ids = []
    scores = {}
    ticket_found = ticket.get("found")
    if isinstance(ticket_found, list):
        for entry in ticket_found:
            if not isinstance(entry, dict):
                continue
            layer_id = entry.get("id")
            meaning = entry.get("meaning")
            if isinstance(layer_id, str) and isinstance(meaning, (int, float)):
                found_ids.append(layer_id)
                scores[layer_id] = meaning

    searches = []
    problems: list[str] = []

    turn_message = prompt["turn"].replace("{layers}", json.dumps(map_layers))
    turn_message = turn_message.replace("{sentence}", sentence)
    message = turn_message

    async with httpx.AsyncClient(timeout=180) as client:
        if not chat_key:
            chat_key = await start_conversation(client)
            message = prompt["instructions"] + turn_message

        step = await ask_for_step(client, chat_key, message, problems)
        if step is None:
            # A chat_key the service no longer knows silently becomes an empty conversation,
            # and a model without its instructions answers in prose. Begin again with them.
            chat_key = await start_conversation(client)
            found_ids = []
            step = await ask_for_step(client, chat_key, prompt["instructions"] + turn_message, problems)

        steps_taken = 1
        while step is not None and step["action"] == "search":
            if steps_taken >= MAX_STEPS_PER_TURN:
                problems.append("the model kept searching and never answered")
                step = None
                break
            steps_taken += 1

            already_searched = False
            for done in searches:
                if done["query"] == step["query"]:
                    already_searched = True

            if already_searched:
                step = await ask_for_step(client, chat_key, ALREADY_SEARCHED, problems)
                continue
            if len(searches) >= MAX_SEARCHES_PER_TURN:
                step = await ask_for_step(client, chat_key, NO_MORE_SEARCHES, problems)
                continue

            found = await search_catalogue(client, step["query"])
            if found["version"] != version:
                # The catalogue changed since the earlier searches, so their ids no longer hold.
                found_ids = []
                scores = {}
                version = found["version"]
            for source in ("geoserver", "emodnet"):
                for layer in found["results"][source]:
                    if layer["id"] not in found_ids:
                        found_ids.append(layer["id"])
                    # A layer found again keeps the score of the latest search.
                    scores[layer["id"]] = layer["meaning"]
            searches.append({"query": found["query"], "nothing_close": found["nothing_close"]})

            step = await ask_for_step(client, chat_key, results_message(found) + NEXT_STEP, problems)

        # The loop ends with the model's answer, or with None when it never gave one.
        answer = step

        operations = []
        pick_ids = []
        reply = ""
        if answer is not None:
            operations = checks.valid_operations(answer["operations"], map_layers)
            pick_ids = checks.valid_picks(answer["layers"], found_ids)
            reply = answer["reply"]
            if answer["operations"] and not operations:
                # The reply may still say a layer was changed, so the caller is told it was not.
                problems.append("the model asked to change layers that are not on the map")
        picks = await picked_layers(client, pick_ids, scores, version)

    found = []
    for layer_id in found_ids:
        found.append({"id": layer_id, "meaning": scores[layer_id]})

    return {
        "operations": operations,
        "picks": picks,
        "reply": reply,
        "searches": searches,
        "conversation": {"chat_key": chat_key, "found": found, "version": version},
        "problems": problems,
    }

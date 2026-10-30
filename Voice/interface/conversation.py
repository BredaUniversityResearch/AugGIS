"""One spoken sentence in a person's conversation with the language model.

The model decides what the sentence asks for. It works in steps, each one line of JSON:

    {"action": "search", "query": "bathymetry"}                 search the layer catalogue
    {"action": "answer", "operations": [...], "layers": [...], "reply": "..."}   finished
    {"action": "download", "id": "E4", "operations": [...], "reply": "..."}   finished, download one layer

After a search the results are sent back to the model and it gives its next step. An
answer can change layers on the map ("operations"), list catalogue layers it found
("layers"), or both, and always says something back ("reply").

A download names one layer a search in this conversation found, and may also change layers
on the map ("hide bathymetry and download the wind farms"). The search service downloads it
for the caller's region, here on the helper, exactly as layers.py does. Only counts and one
line of text go back to the caller (download_result); the downloaded layer itself is thrown
away, so nothing is written, kept or sent to a headset.

The language model is the BUas AI Assistants API. It keeps each conversation itself, under a
chat_key, so only the new sentence is sent. The caller holds a small ticket and hands it back
with its next sentence:

    {"chat_key": "...", "found": [{"id": "G12", "meaning": 0.84}, ...], "version": "e9c62ca3ba54"}

"found" is every catalogue layer a search in this conversation returned, with how close its
meaning was to the search words. A follow-up such as "only the Baltic ones" may pick from
those without searching again. After a download the ticket also carries "last_download", its
result line, which is put in front of the next sentence so the model knows how it went. This service keeps
nothing between sentences, so each caller has its own conversation and a restart loses none.
"""

import asyncio
import json
import os
import time

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
NOT_FOUND_YET = ("No search in this conversation found a layer with the id {id}. Download only an id from "
                 + "your search results, or search first.")
# Put in front of the next sentence, so the model knows how its last download went.
LAST_DOWNLOAD = ("Your last download gave: {line}\nIf the user asks whether it downloaded, answer yes or no "
                 + "from that line, plainly.\n")
# A refusal's line starts with this, so the panel and the model both read the outcome first.
NOT_DOWNLOADED = "Not downloaded. "
# Sent back when the model never gave a step it was allowed to, so the panel does not stay blank.
NO_STEP_REPLY = "Sorry, I could not work that out, so nothing was changed or downloaded. Please say it again."

# The longest download line kept in the ticket. The ticket comes back from the caller, so its
# text goes into the model's message only this long.
MAX_LAST_DOWNLOAD = 300

# The search service allows a big layer up to 5 minutes, which layers.py uses through /download.
DOWNLOAD_TIMEOUT = 330
# A spoken download is stopped sooner, so its result line reaches the headset before the
# headset's own 120 s timeout. The biggest group measured took about 20 s.
DOWNLOAD_DEADLINE = 75

SERVER_WORDS = {"geoserver": "team GeoServer", "emodnet": "EMODnet"}


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


async def search_catalogue(client: httpx.AsyncClient, query: str, region: list[float] | None) -> dict:
    """Ask the search service for the layers closest to the model's search words.

    With a region, each result also says whether its layer's area overlaps the map ("on_map").
    """
    body = {"query": query, "per_server": RESULTS_PER_SERVER}
    if region is not None:
        body["region"] = region
    response = await client.post(f"{SEARCH_URL}/search", json=body)
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
            on_map = layer.get("on_map", "unknown")
            lines.append(layer["id"] + ": " + layer["spoken"] + " (" + layer["group"] + "), closeness "
                         + format(layer["meaning"], ".2f") + ", on the map's area: " + on_map)
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


def where_from(layer: dict) -> str:
    """The sea or theme and the server of a catalogue layer, "North Sea, team GeoServer".

    Several layers share a title ("Wind Farms" is on 4 seas), so a line names both.
    """
    parts = []
    group = layer.get("group")
    if isinstance(group, str) and group:
        parts.append(group)
    server = SERVER_WORDS.get(layer.get("source"), "")
    if server:
        parts.append(server)
    return ", ".join(parts)


def source_of(layer_id: str) -> str:
    """The server an id is on, from its first letter: G is the team GeoServer, E is EMODnet."""
    if layer_id.upper().startswith("G"):
        return "geoserver"
    if layer_id.upper().startswith("E"):
        return "emodnet"
    return ""


def download_result(layer: dict, status: int, body: dict, took_ms: int) -> dict:
    """The result of one download as it goes back to the caller: counts and one line of text.

    This is the only place the result and its line are made. Nothing of the downloaded layer
    itself (the GeoJSON or the PNG) is copied into it.

    Args:
        layer: The catalogue entry that was downloaded: "source", "name", "spoken" and "group".
        status: The status the search service's /download answered with, 0 if it did not answer.
        body: Its JSON answer: the downloaded layer, or {"error": the refusal sentence}.
        took_ms: How long the download took.

    Returns:
        {"ok", "source", "name", "title", "kind", "features", "line", "took_ms"}.
        A refusal has "ok" false, "kind" "" and its sentence as "line".

    Raises:
        KeyError: If a 200 answer lacks a field the line needs.
    """
    result = {
        "ok": False,
        "source": layer["source"],
        "name": layer["name"],
        "title": layer["spoken"],
        "kind": "",
        "features": 0,
        "line": "",
        "took_ms": took_ms,
    }
    where = where_from(layer)

    if status != 200:
        error = body.get("error")
        if not isinstance(error, str) or not error:
            error = "The download of " + layer["spoken"] + " failed (HTTP " + str(status) + ")."
        if where:
            error = error + " (" + where + ")"
        result["line"] = NOT_DOWNLOADED + error
        return result

    title = body["title"]
    described = title
    if where:
        described = title + " (" + where + ")"
    start = "Downloaded " + described + " on the helper: "

    if body["kind"] == "picture":
        line = start + "picture " + str(body["width"]) + "×" + str(body["height"]) + "."
    else:
        line = start + str(body["features"]) + " shapes."

    result["ok"] = True
    result["kind"] = body["kind"]
    result["title"] = title
    if body["kind"] != "picture":
        result["features"] = body["features"]
    result["line"] = line
    return result


async def download_found(layer_id: str, version: str, region: list[float] | None, region_problem: str = "") -> dict:
    """Download a layer a search found, here on the helper, and keep only its download_result.

    Every way it can go wrong, the search service down included, comes back as a result with
    "ok" false, so the caller always gets a line to show.

    Args:
        layer_id: The id, which a search in this conversation returned.
        version: The catalogue version that search was in.
        region: [minx, miny, maxx, maxy] in EPSG:3035, or None when the caller's was unreadable.
        region_problem: What was wrong with the caller's region, when region is None.
    """
    nobody = {"source": source_of(layer_id), "name": "", "spoken": layer_id, "group": ""}

    async with httpx.AsyncClient(timeout=DOWNLOAD_TIMEOUT) as client:
        try:
            response = await client.post(f"{SEARCH_URL}/layers", json={"ids": [layer_id]})
            response.raise_for_status()
            looked_up = response.json()
            looked_up_version = looked_up["version"]
            looked_up_layers = looked_up["layers"]
        except (httpx.HTTPError, ValueError, KeyError, TypeError) as exc:
            message = ("The download of " + layer_id + " did not start: the search service could not look it up ("
                       + type(exc).__name__ + ").")
            return download_result(nobody, 0, {"error": message}, 0)

        if looked_up_version != version:
            # The ids now mean other layers, so downloading this one would fetch the wrong layer.
            message = "The layer list has changed since that search, so " + layer_id + " is another layer now. Search again."
            return download_result(nobody, 409, {"error": message}, 0)
        if not looked_up_layers:
            message = "The layer list has no layer " + layer_id + ". Search again."
            return download_result(nobody, 404, {"error": message}, 0)
        layer = looked_up_layers[0]

        if region is None:
            message = "Nothing was downloaded, because the map's region the headset sent is unreadable: " + region_problem
            return download_result(layer, 400, {"error": message}, 0)

        body = {"source": layer["source"], "name": layer["name"], "region": region}
        started = time.perf_counter()
        try:
            response = await asyncio.wait_for(client.post(f"{SEARCH_URL}/download", json=body), DOWNLOAD_DEADLINE)
        except TimeoutError:
            took_ms = round((time.perf_counter() - started) * 1000)
            message = ("The download of " + layer["spoken"] + " took longer than " + str(DOWNLOAD_DEADLINE)
                       + " seconds, so the helper stopped it.")
            return download_result(layer, 0, {"error": message}, took_ms)
        except httpx.HTTPError as exc:
            took_ms = round((time.perf_counter() - started) * 1000)
            message = ("The download of " + layer["spoken"] + " did not finish: the search service did not answer ("
                       + type(exc).__name__ + ").")
            return download_result(layer, 0, {"error": message}, took_ms)
        took_ms = round((time.perf_counter() - started) * 1000)

    try:
        answer = response.json()
    except ValueError:
        answer = {}
    if not isinstance(answer, dict):
        answer = {}
    try:
        return download_result(layer, response.status_code, answer, took_ms)
    except (KeyError, TypeError) as exc:
        message = ("The download of " + layer["spoken"] + " came back without its counts ("
                   + type(exc).__name__ + " " + str(exc) + ").")
        return download_result(layer, 0, {"error": message}, took_ms)


async def turn(sentence: str, map_layers: list[str], ticket: dict, region: list[float] | None = None,
               region_problem: str = "") -> dict:
    """Continue a person's conversation with one new sentence.

    Args:
        sentence: What the person just said.
        map_layers: The names of the layers in the person's session, which may be shown or hidden.
        ticket: The "conversation" from the previous answer, or {} to start a new one.
        region: The session's [minx, miny, maxx, maxy] in EPSG:3035, for a download.
            None means checks.DEFAULT_REGION, unless region_problem says why it is missing.
        region_problem: What was wrong with the caller's region. Only a download is refused for
            it; showing, hiding and searching still work.

    Returns:
        {"operations": [{"action": "hide", "layers": [...]}, ...] to apply to the map,
         "picks": {"geoserver": [...], "emodnet": [...]} catalogue layers found for the person,
         "reply": what the model says back, or for a download its result line,
         "downloads": [download_result, ...], 0 or 1 of them,
         "searches": [{"query", "nothing_close"}, ...] run during this turn,
         "conversation": the ticket to send back with the next sentence,
         "problems": what went wrong on the way, such as a reply that was not a step. It can
             have entries although the turn worked, when a second try succeeded}

    Raises:
        httpx.HTTPError: If the language model or the search service did not answer. A
            download that fails is a result with "ok" false instead.
        RuntimeError: If the language model refused the keys.
    """
    prompt = load_voice_prompt()
    if region is None and not region_problem:
        region = list(checks.DEFAULT_REGION)

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
    # The model never sees a download's result in the turn that did it, so it is told now.
    last_download = ticket.get("last_download")
    if isinstance(last_download, str) and last_download.strip():
        note = last_download.strip()[:MAX_LAST_DOWNLOAD]
        turn_message = LAST_DOWNLOAD.replace("{line}", note) + turn_message
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
        download_id = None
        while step is not None and step["action"] != "answer":
            if step["action"] == "download":
                download_id = checks.valid_download(step["id"], found_ids)
                if download_id is not None:
                    break

            if steps_taken >= MAX_STEPS_PER_TURN:
                if step["action"] == "download":
                    problems.append("the model kept asking to download layers no search found, and never answered")
                else:
                    problems.append("the model kept searching and never answered")
                step = None
                break
            steps_taken += 1

            if step["action"] == "download":
                problems.append("the model asked to download " + step["id"] + ", which no search in this "
                                + "conversation found")
                message = NOT_FOUND_YET.replace("{id}", step["id"]) + NEXT_STEP
                step = await ask_for_step(client, chat_key, message, problems)
                continue

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

            found = await search_catalogue(client, step["query"], region)
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

        # The loop ends with the model's answer, a download of a found layer, or None when it
        # never gave either.
        answer = step

        operations = []
        pick_ids = []
        reply = NO_STEP_REPLY
        downloads = []
        if answer is not None:
            operations = checks.valid_operations(answer["operations"], map_layers)
            if answer["operations"] and not operations:
                # The reply may still say a layer was changed, so the caller is told it was not.
                problems.append("the model asked to change layers that are not on the map")
        if answer is not None and answer["action"] == "download":
            result = await download_found(download_id, version, region, region_problem)
            downloads.append(result)
            reply = result["line"]
        elif answer is not None:
            pick_ids = checks.valid_picks(answer["layers"], found_ids)
            reply = answer["reply"]
        picks = await picked_layers(client, pick_ids, scores, version)

    found = []
    for layer_id in found_ids:
        found.append({"id": layer_id, "meaning": scores[layer_id]})

    ticket_out = {"chat_key": chat_key, "found": found, "version": version}
    if downloads:
        ticket_out["last_download"] = downloads[0]["line"]

    return {
        "operations": operations,
        "picks": picks,
        "reply": reply,
        "downloads": downloads,
        "searches": searches,
        "conversation": ticket_out,
        "problems": problems,
    }

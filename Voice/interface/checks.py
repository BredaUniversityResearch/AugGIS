"""Check what the language model answered before any of it reaches the map.

The model is asked for one line of JSON in words. Nothing forces it to keep to that, or to
name only layers that exist, so every answer goes through here first:

    parse_step        is the reply one of the three steps at all?
    valid_operations  show, hide and toggle only, and only on layers that are on the map
    valid_picks       only layers that a search in this conversation really returned
    valid_download    the same for the one layer a download names

parse_region checks the region the headset sends with its sentence.

These functions take and return plain values and call nothing, so they can be tested alone.
"""

import json
import math
import re

LAYER_ACTIONS = ("show", "hide", "toggle")
MAX_PICKS = 10

# The Belgian and southern Dutch coast, EPSG:3035, easting first: the region used when the
# headset sends none. The same box as download.DEFAULT_REGION in the search service.
DEFAULT_REGION = [3797132.0, 3137432.0, 3965925.0, 3291676.0]

# Words Whisper adds when it reads a list aloud.
FILLER_WORDS = ("and", "the", "a", "or")
# A recital is at least this many expected names in a row. A judgement, not a measurement.
MIN_NAMES_IN_ECHO = 3

# The JSON is taken from the first "{" to the last "}", which also copes with a reply the
# model wrapped in a code block or put a sentence in front of.
JSON_OBJECT = re.compile(r"\{.*\}", re.DOTALL)


def read_json(text: str):
    """The JSON in text, or None.

    The BUas model sometimes doubles every brace, {{"action": ..., [{{...}}]}}, as if it were
    filling in a template, so a reply that starts with {{ is read again with each pair made one.
    """
    try:
        return json.loads(text)
    except ValueError:
        pass
    if not text.startswith("{{"):
        return None
    single = text.replace("{{", "{").replace("}}", "}")
    try:
        return json.loads(single)
    except ValueError:
        return None


def parse_step(reply: str) -> dict | None:
    """Read a step out of the model's reply, or None if it is not one.

    Returns:
        {"action": "search", "query": str},
        {"action": "answer", "operations": list, "layers": list, "reply": str} or
        {"action": "download", "id": str, "operations": list, "reply": str}.
        The lists and the id are what the model wrote, not yet checked.
    """
    match = JSON_OBJECT.search(reply)
    if match is None:
        return None
    step = read_json(match.group(0))
    if not isinstance(step, dict):
        return None

    if step.get("action") == "search":
        query = step.get("query")
        if not isinstance(query, str) or not query.strip():
            return None
        return {"action": "search", "query": query.strip()}

    if step.get("action") == "answer":
        operations = step.get("operations")
        if not isinstance(operations, list):
            operations = []
        layers = step.get("layers")
        if not isinstance(layers, list):
            layers = []
        said = step.get("reply")
        if not isinstance(said, str):
            said = ""
        return {"action": "answer", "operations": operations, "layers": layers, "reply": said}

    if step.get("action") == "download":
        layer_id = step.get("id")
        if not isinstance(layer_id, str) or not layer_id.strip():
            return None
        # A sentence such as "hide bathymetry and download the wind farms" also changes the map.
        operations = step.get("operations")
        if not isinstance(operations, list):
            operations = []
        said = step.get("reply")
        if not isinstance(said, str):
            said = ""
        return {"action": "download", "id": layer_id.strip(), "operations": operations, "reply": said}

    return None


def valid_operations(raw_operations: list, map_layers: list[str]) -> list[dict]:
    """Keep the operations that name a real action and at least one layer on the map.

    A name is matched without regard to capitals or spaces around it, and returned in the
    map's own spelling, so a model that writes "bathymetry" for "Bathymetry" still hides the
    right layer. The action is matched without regard to capitals too.

    Args:
        raw_operations: The "operations" list as the model wrote it.
        map_layers: The names of the layers in the caller's session.

    Returns:
        [{"action": "hide", "layers": ["Bathymetry"]}, ...], possibly empty.
    """
    spelling = {}
    for name in map_layers:
        spelling[name.lower()] = name

    operations = []
    for raw in raw_operations:
        if not isinstance(raw, dict):
            continue
        action = raw.get("action")
        if not isinstance(action, str) or action.lower() not in LAYER_ACTIONS:
            continue
        raw_layers = raw.get("layers")
        if not isinstance(raw_layers, list):
            continue

        layers = []
        for name in raw_layers:
            if not isinstance(name, str):
                continue
            on_map = spelling.get(name.strip().lower())
            if on_map is not None and on_map not in layers:
                layers.append(on_map)

        if layers:
            operations.append({"action": action.lower(), "layers": layers})
    return operations


def valid_picks(raw_ids: list, found_ids: list[str]) -> list[str]:
    """Keep the picked ids that a search in this conversation returned, each once.

    Args:
        raw_ids: The "layers" list as the model wrote it.
        found_ids: Every id any search in the conversation has returned.

    Returns:
        Up to MAX_PICKS ids, in the model's order.
    """
    picks = []
    for layer_id in raw_ids:
        if layer_id in found_ids and layer_id not in picks:
            picks.append(layer_id)
    return picks[:MAX_PICKS]


def valid_download(raw_id: str, found_ids: list[str]) -> str | None:
    """The id a download step names, if a search in this conversation returned it.

    Matched without regard to capitals, because the model may write "e4" for "E4".

    Returns:
        The id as the search wrote it, or None.
    """
    for layer_id in found_ids:
        if layer_id.lower() == raw_id.strip().lower():
            return layer_id
    return None


def parse_region(text: str) -> tuple[list[float] | None, str]:
    """Read the "minx,miny,maxx,maxy" region the headset sends, in EPSG:3035.

    Args:
        text: The form field as sent. Empty means DEFAULT_REGION.

    Returns:
        (the 4 numbers, "") or (None, a sentence saying what is wrong with it).
    """
    if not text.strip():
        return list(DEFAULT_REGION), ""

    parts = text.split(",")
    if len(parts) != 4:
        return None, "region must be 4 numbers, minx,miny,maxx,maxy in EPSG:3035, not " + repr(text)

    region = []
    for part in parts:
        try:
            number = float(part)
        except ValueError:
            return None, "region must be 4 numbers, minx,miny,maxx,maxy in EPSG:3035, not " + repr(text)
        if not math.isfinite(number):
            return None, "region must be 4 finite numbers, not NaN or infinity: " + repr(text)
        region.append(number)

    if region[0] >= region[2] or region[1] >= region[3]:
        return None, "region must be minx,miny,maxx,maxy, with each min smaller than its max: " + repr(text)
    return region, ""


def is_list_echo(transcript: str, expected: list[str]) -> bool:
    """Detect Whisper reciting its list of expected names instead of transcribing speech.

    Whisper cannot report hearing nothing. Given near-silence and a list of names to expect,
    the most plausible text is that list. Measured on the headset: a 0.16 s clip at peak
    0.001 came back as the layer list.

    A recital follows the list: the same names, in the same order, and nothing else. So a
    transcript is an echo when it is three or more of the expected names in a row, in the
    order Whisper was given them. A person who names layers picks their own, in their own
    order, which is why "bathymetry and wind farms" is speech even when both are expected.

    Args:
        transcript: The text Whisper returned.
        expected: The names Whisper was told to expect, in the order it was given them.
    """
    spoken = []
    for word in re.findall(r"[a-z0-9]+", transcript.lower()):
        if word not in FILLER_WORDS:
            spoken.append(word)
    if not spoken:
        return False

    names = []
    for name in expected:
        names.append(re.findall(r"[a-z0-9]+", name.lower()))

    for start in range(len(names)):
        recited = []
        for count in range(1, len(names) - start + 1):
            recited = recited + names[start + count - 1]
            if len(recited) > len(spoken):
                break
            if count >= MIN_NAMES_IN_ECHO and recited == spoken:
                return True
    return False

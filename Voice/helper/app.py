"""AugGIS voice helper.

Takes a WAV recording plus the layer names that exist in the caller's session, and
returns the layer operations the speaker asked for.

    POST /command   multipart/form-data
        audio   the WAV file, 16 kHz mono PCM
        layers  JSON array of layer names
        trim    optional, "false" turns the silence filter off

    200 -> {
      "transcript": "turn off bathymetry and show shipping lanes",
      "operations": [
        {"action": "hide", "layers": ["Bathymetry"]},
        {"action": "show", "layers": ["Shipping lanes"]}
      ],
      "timings_ms": {"stt": 312, "llm": 154, "total": 480}
    }

The caller sends its own layer names because the layers in a session come from whatever
config was exported, so they are not known when this service is built or started. The
JSON schema's enum is built from that list on every request, which is what stops the
model naming a layer that does not exist. The command-line client sends names typed as
an argument. Unity will send LayerManager.AllLayers. Same endpoint either way.

Whisper and the LLM are only reachable from inside the compose network. The caller talks
to this service and nothing else, so changing the prompt does not touch the caller.
"""

import json
import logging
import os
import time

import httpx
from fastapi import FastAPI, File, Form, UploadFile
from fastapi.responses import JSONResponse

WHISPER_URL = os.getenv("WHISPER_URL", "http://whisper:9000")
LLM_URL = os.getenv("LLM_URL", "http://llm:11434")
LLM_MODEL = os.getenv("LLM_MODEL", "llama3.2:3b")
PROMPT_PATH = os.getenv("PROMPT_PATH", "/app/prompt.json")

ACTIONS = ["show", "hide", "toggle", "show_only", "none"]

# Spellings of "off" accepted for the trim field. Callers write booleans differently and
# a form field is always text, so a service that accepts only one spelling treats the
# rest as "on" without saying so.
FALSE_WORDS = ("0", "false", "off", "no")

# Words Whisper adds when it reads a list aloud. Allowed in an echo without making the
# transcript count as speech.
FILLER_WORDS = {"and", "the", "a", "or"}

logging.basicConfig(level=logging.INFO, format="%(asctime)s %(levelname)s %(message)s")
log = logging.getLogger("voice-helper")

app = FastAPI(title="AugGIS voice helper")


def build_schema(layers: list[str]) -> dict:
    """Build the JSON schema the model's reply is constrained to.

    Ollama turns the schema into a grammar and masks any token that would break it, so
    a layer name outside the enum cannot be produced. This fixes the vocabulary, not the
    choice: the few-shot examples in prompt.json are what make it pick the right layer.

    Args:
        layers: The caller's real layer names, used as the enum.

    Returns:
        A JSON schema describing a list of operations.
    """
    return {
        "type": "object",
        "properties": {
            "operations": {
                "type": "array",
                "items": {
                    "type": "object",
                    "properties": {
                        "action": {"type": "string", "enum": ACTIONS},
                        "layers": {"type": "array", "items": {"type": "string", "enum": layers}},
                    },
                    "required": ["action", "layers"],
                },
            }
        },
        "required": ["operations"],
    }


def resolve_operations(raw_ops: list[dict], layer_names: list[str]) -> list[dict]:
    """Clean the model's operations into ones a client can apply directly.

    Drops names that are not the caller's, collapses repeats, and removes operations
    that end up with nothing to act on. The repeat check is not padding: asked to turn
    everything off except one layer, the model returned "Wind farms" twice. The grammar
    controls which names are legal, not how many times one can appear.

    Args:
        raw_ops: The operations list from the model.
        layer_names: The caller's real layer names.

    Returns:
        A list of operations, each naming at least one real layer. Never empty; falls
        back to a single "none" operation so the caller always gets the same shape.
    """
    resolved: list[dict] = []

    for op in raw_ops or []:
        layers: list[str] = []
        for name in op.get("layers") or []:
            if name in layer_names and name not in layers:
                layers.append(name)

        # "none" means the speaker named no layer, so there is nothing to send on. An
        # operation with no surviving layers is the same situation.
        action = op.get("action", "none")
        if action == "none" or not layers:
            continue

        resolved.append({"action": action, "layers": layers})

    if not resolved:
        return [{"action": "none", "layers": []}]
    return resolved


def _tokens(text: str) -> set[str]:
    """Split text into lowercase words, dropping punctuation.

    Args:
        text: Any string.

    Returns:
        The set of words found. "Shipping lanes, Wind farms!" gives
        {"shipping", "lanes", "wind", "farms"}.
    """
    letters_only = []
    for c in text:
        if c.isalnum():
            letters_only.append(c.lower())
        else:
            letters_only.append(" ")
    return set("".join(letters_only).split())


def is_prompt_echo(transcript: str, layer_names: list[str]) -> bool:
    """Detect Whisper repeating the layer names back instead of transcribing speech.

    Whisper cannot report hearing nothing, it always produces plausible text. Given
    near-silence and the layer names as an initial_prompt, the most plausible text is
    the layer names. Measured on the headset: a 0.16 s clip at peak 0.001 came back as
    the layer list, and the layers were switched on with nobody speaking.

    A real command contains a verb, and no verb appears in a layer name. So a transcript
    made only of layer-name words was not spoken.

    Args:
        transcript: The text Whisper returned.
        layer_names: The caller's real layer names.

    Returns:
        True if the transcript looks like the layer list rather than speech. False for
        an empty transcript, which is handled separately.
    """
    words = _tokens(transcript)
    if not words:
        return False

    allowed = set()
    for name in layer_names:
        allowed |= _tokens(name)
    allowed |= FILLER_WORDS

    return words <= allowed


def build_messages(prompt: dict, layer_names: list[str], transcript: str) -> list[dict]:
    """Build the chat messages sent to the model.

    The examples are sent as a conversation the model appears to have already had and
    got right, because continuing that pattern is then the easiest thing available to
    it. The layer names go in the system message as well as the schema's enum: the
    grammar forbids a wrong name but cannot tell the model what the real ones are, and
    it needs to know them to answer "none" to a layer that does not exist.

    Args:
        prompt: The parsed prompt.json, with "system" and "examples" keys.
        layer_names: The caller's real layer names.
        transcript: What the speaker said.

    Returns:
        The messages list for the chat request, with the transcript last.
    """
    system = prompt["system"] + "\n\nAvailable layers: " + json.dumps(layer_names)
    messages = [{"role": "system", "content": system}]

    for example in prompt.get("examples", []):
        messages.append({"role": "user", "content": example["user"]})
        messages.append({"role": "assistant", "content": json.dumps(example["assistant"])})

    messages.append({"role": "user", "content": transcript})
    return messages


def load_prompt() -> dict:
    """Read prompt.json from disk.

    Read on every request rather than cached at startup. The file is bind-mounted, so
    editing it and restarting the container applies in about 3 seconds with no rebuild.

    Returns:
        The parsed prompt file.
    """
    with open(PROMPT_PATH, encoding="utf-8") as fh:
        return json.load(fh)


async def transcribe(wav: bytes, filename: str, layer_names: list[str], vad: bool) -> str:
    """Send audio to Whisper and return what was said.

    Args:
        wav: The WAV file contents.
        filename: Name to send with the upload.
        layer_names: Fed in as initial_prompt so the layer names are expected
            vocabulary. Without it "Bathymetry" comes back as "bath symmetry".
        vad: Whether to run Whisper's silence filter.

    Returns:
        The transcript, stripped. Empty if Whisper heard nothing.

    Raises:
        httpx.HTTPError: If Whisper is unreachable or returns an error.
    """
    async with httpx.AsyncClient(timeout=120) as client:
        r = await client.post(
            f"{WHISPER_URL}/asr",
            params={
                "encode": "true",
                "task": "transcribe",
                "language": "en",  # a short noisy clip is where auto-detect guesses wrong
                "output": "json",
                "initial_prompt": ", ".join(layer_names),
                "vad_filter": "true" if vad else "false",
            },
            files={"audio_file": (filename, wav, "audio/wav")},
        )
    r.raise_for_status()
    return (r.json().get("text") or "").strip()


async def resolve_intent(transcript: str, layer_names: list[str]) -> list[dict]:
    """Send the transcript to the model and return the operations it chose.

    Args:
        transcript: What the speaker said.
        layer_names: The caller's real layer names, used for the enum and the prompt.

    Returns:
        The raw operations list from the model, before cleaning.

    Raises:
        httpx.HTTPError: If the model service is unreachable or returns an error.
    """
    async with httpx.AsyncClient(timeout=120) as client:
        r = await client.post(
            f"{LLM_URL}/api/chat",
            json={
                "model": LLM_MODEL,
                "stream": False,
                "keep_alive": -1,  # Ollama unloads after 5 min idle, costing 20 s mid-session
                "format": build_schema(layer_names),
                "options": {"temperature": 0},  # same audio, same answer, every time
                "messages": build_messages(load_prompt(), layer_names, transcript),
            },
        )
    r.raise_for_status()
    return json.loads(r.json()["message"]["content"]).get("operations")


@app.get("/health")
async def health():
    """Report whether both model services are actually reachable.

    Returns:
        A dict naming each service and its state, plus "ready" when both answer. Each is
        named separately so a caller can say which one is missing.
    """
    out = {"helper": "ok"}
    async with httpx.AsyncClient(timeout=5) as client:
        for name, url in (("whisper", f"{WHISPER_URL}/docs"), ("llm", f"{LLM_URL}/api/tags")):
            try:
                r = await client.get(url)
                out[name] = "ok" if r.status_code < 400 else f"http {r.status_code}"
            except Exception as exc:
                # The kind of error is the useful part: "connection refused" means nothing
                # is listening, "timed out" means something is listening but stuck.
                out[name] = f"unreachable ({type(exc).__name__})"

    out["ready"] = out.get("whisper") == "ok" and out.get("llm") == "ok"
    return out


@app.post("/command")
async def command(audio: UploadFile = File(...), layers: str = Form(...), trim: str = Form("true")):
    """Turn a recording into layer operations.

    Args:
        audio: The WAV file, 16 kHz mono PCM.
        layers: JSON array of the caller's layer names.
        trim: "false", "0", "off" or "no" turns Whisper's silence filter off.

    Returns:
        The transcript, the resolved operations, and timings for each stage. 400 if the
        layer list is wrong, which the caller must fix. 503 if a model service did not
        answer, which the caller can retry. A 503 from the model still carries the
        transcript, so the caller can show what was heard.
    """
    started = time.perf_counter()
    vad = str(trim).strip().lower() not in FALSE_WORDS

    # Validate before doing any work, so a bad request costs microseconds rather than a
    # full transcription.
    try:
        layer_names = json.loads(layers)
    except ValueError:
        return JSONResponse({"error": "layers must be a JSON array of strings"}, status_code=400)

    if not isinstance(layer_names, list) or not all(isinstance(x, str) for x in layer_names):
        return JSONResponse({"error": "layers must be a JSON array of strings"}, status_code=400)

    if not layer_names:
        # An empty enum is a grammar the model cannot satisfy.
        return JSONResponse({"error": "layers array is empty"}, status_code=400)

    wav = await audio.read()

    stage = time.perf_counter()
    try:
        transcript = await transcribe(wav, audio.filename or "a.wav", layer_names, vad)
    except Exception as exc:
        log.exception("whisper failed")
        return JSONResponse({"error": f"speech-to-text unavailable: {exc}"}, status_code=503)
    stt_ms = round((time.perf_counter() - stage) * 1000)

    if is_prompt_echo(transcript, layer_names):
        log.info("prompt echo suppressed: %r", transcript)
        transcript = ""

    if not transcript:
        # Same response shape as a success, so the caller has no special case to write.
        # The zero is honest: the model step did not run.
        return {
            "transcript": "",
            "operations": [{"action": "none", "layers": []}],
            "timings_ms": {
                "stt": stt_ms,
                "llm": 0,
                "total": round((time.perf_counter() - started) * 1000),
            },
        }

    stage = time.perf_counter()
    try:
        raw_ops = await resolve_intent(transcript, layer_names)
    except Exception as exc:
        log.exception("llm failed")
        return JSONResponse(
            {"error": f"intent service unavailable: {exc}", "transcript": transcript},
            status_code=503,
        )
    llm_ms = round((time.perf_counter() - stage) * 1000)

    operations = resolve_operations(raw_ops, layer_names)
    result = {
        "transcript": transcript,
        "operations": operations,
        "timings_ms": {
            "stt": stt_ms,
            "llm": llm_ms,
            "total": round((time.perf_counter() - started) * 1000),
        },
    }

    log.info("%r -> %s (%dms)", transcript, operations, result["timings_ms"]["total"])
    return result

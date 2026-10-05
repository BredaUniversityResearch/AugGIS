"""AugGIS interface.

The one door for the headset: takes a spoken sentence and answers with what to do on the map
and what was found in the online layer catalogue.

    POST /voice   multipart/form-data
        audio         the WAV file, 16 kHz mono PCM (or `text`, the sentence typed)
        layers        JSON array of the names of the layers on the map
        conversation  the "conversation" object from the previous answer, or {} to start
        trim          optional, "false" turns the silence filter off

    200 -> {
      "transcript": "hide bathymetry and find wind farms",
      "operations": [{"action": "hide", "layers": ["Bathymetry"]}],
      "picks": {"geoserver": [{"id": "G129", "spoken": "Wind Farms", "group": "Baltic Sea",
                               "name": "balticline:BS_Wind_Farms", "meaning": 0.835}, ...],
                "emodnet": [...]},
      "reply": "I hid Bathymetry and found wind farm layers for the Baltic Sea ...",
      "searches": [{"query": "wind farms", "nothing_close": false}],
      "conversation": {"chat_key": "...", "found": [{"id": "G129", "meaning": 0.835}], "version": "..."},
      "problems": [],
      "timings_ms": {"stt": 650, "llm": 5400, "total": 6100}
    }

    GET /health   whether speech-to-text, the embedding model and the search answer

Whisper, the embedding model and the search service are only reachable from inside the
compose network. The language model is the BUas AI Assistants API, over the internet.
"""

import json
import logging
import os
import time

import httpx
from fastapi import FastAPI, File, Form, UploadFile
from fastapi.responses import JSONResponse

import checks
import conversation

WHISPER_URL = os.getenv("WHISPER_URL", "http://whisper:9000")
LLM_URL = os.getenv("LLM_URL", "http://llm:11434")

# Spellings of "off" accepted for the trim field. Callers write booleans differently and
# a form field is always text, so a service that accepts only one spelling treats the
# rest as "on" without saying so.
FALSE_WORDS = ("0", "false", "off", "no")

logging.basicConfig(level=logging.INFO, format="%(asctime)s %(levelname)s %(message)s")
log = logging.getLogger("interface")

app = FastAPI(title="AugGIS interface")


async def transcribe(wav: bytes, filename: str, expected: list[str], vad: bool) -> str:
    """Send audio to Whisper and return what was said.

    Args:
        wav: The WAV file contents.
        filename: Name to send with the upload.
        expected: Words fed in as initial_prompt, so they are expected vocabulary. Without
            them "Bathymetry" comes back as "bath symmetry".
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
                "initial_prompt": ", ".join(expected),
                "vad_filter": "true" if vad else "false",
            },
            files={"audio_file": (filename, wav, "audio/wav")},
        )
    r.raise_for_status()
    return (r.json().get("text") or "").strip()


@app.get("/health")
async def health():
    """Report whether each service this one depends on actually answers.

    Returns:
        A dict naming each service and its state, plus "ready" when all answer. Each is
        named separately so a caller can say which one is missing.
    """
    out = {"interface": "ok"}
    async with httpx.AsyncClient(timeout=5) as client:
        for name, url in (("whisper", f"{WHISPER_URL}/docs"), ("llm", f"{LLM_URL}/api/tags")):
            try:
                r = await client.get(url)
                out[name] = "ok" if r.status_code < 400 else f"http {r.status_code}"
            except Exception as exc:
                # The kind of error is the useful part: "connection refused" means nothing
                # is listening, "timed out" means something is listening but stuck.
                out[name] = f"unreachable ({type(exc).__name__})"

        # The search service is only ready once it has fetched and prepared the layer list.
        try:
            r = await client.get(f"{conversation.SEARCH_URL}/health")
            search_ready = r.status_code < 400 and r.json().get("ready") is True
            out["search"] = "ok" if search_ready else "preparing the layer list"
        except Exception as exc:
            out["search"] = f"unreachable ({type(exc).__name__})"

    out["ready"] = out.get("whisper") == "ok" and out.get("llm") == "ok" and out.get("search") == "ok"
    return out


@app.post("/voice")
async def voice(
    layers: str = Form("[]"),
    conversation_ticket: str = Form("{}", alias="conversation"),
    audio: UploadFile | None = File(None),
    text: str = Form(""),
    trim: str = Form("true"),
):
    """Take one spoken sentence and answer with what to do on the map and what was found.

    The language model decides whether the sentence asks to show or hide layers that are on
    the map, to search the online catalogue, or both.

    Args:
        layers: JSON array of the names of the layers in the caller's session.
        conversation_ticket: The "conversation" from the previous answer, or {} to start one.
        audio: The WAV file, 16 kHz mono PCM. Leave out to send text instead.
        text: The sentence as text, used when there is no audio.
        trim: "false", "0", "off" or "no" turns Whisper's silence filter off.

    Returns:
        The transcript, the operations to apply to the map, the catalogue layers found per
        server, the reply to show, the conversation to send back next time, and timings.
        400 if a field is wrong, 503 if speech-to-text, the language model or the search did
        not answer. A 503 after speech-to-text still carries the transcript.
    """
    started = time.perf_counter()

    try:
        map_layers = json.loads(layers)
    except ValueError:
        map_layers = None
    if not isinstance(map_layers, list) or not all(isinstance(x, str) for x in map_layers):
        return JSONResponse({"error": "layers must be a JSON array of strings"}, status_code=400)

    try:
        ticket = json.loads(conversation_ticket)
    except ValueError:
        ticket = None
    if not isinstance(ticket, dict):
        return JSONResponse({"error": "conversation must be a JSON object"}, status_code=400)

    transcript = text.strip()
    stt_ms = 0
    if audio is None and not transcript:
        return JSONResponse({"error": "send audio or text"}, status_code=400)

    if audio is not None:
        expected = map_layers + conversation.load_voice_prompt()["whisper_vocabulary"]
        vad = str(trim).strip().lower() not in FALSE_WORDS
        stage = time.perf_counter()
        try:
            transcript = await transcribe(await audio.read(), audio.filename or "a.wav", expected, vad)
        except Exception as exc:
            log.exception("whisper failed")
            return JSONResponse({"error": f"speech-to-text unavailable: {exc}"}, status_code=503)
        stt_ms = round((time.perf_counter() - stage) * 1000)

        if checks.is_list_echo(transcript, expected):
            log.info("list echo suppressed: %r", transcript)
            transcript = ""

    if not transcript:
        return {
            "transcript": "",
            "operations": [],
            "picks": {"geoserver": [], "emodnet": []},
            "reply": "",
            "searches": [],
            "conversation": ticket,
            "problems": [],
            "timings_ms": {"stt": stt_ms, "llm": 0, "total": round((time.perf_counter() - started) * 1000)},
        }

    stage = time.perf_counter()
    try:
        result = await conversation.turn(transcript, map_layers, ticket)
    except Exception as exc:
        log.exception("conversation failed")
        return JSONResponse({"error": f"language model or search unavailable: {exc}", "transcript": transcript},
                            status_code=503)
    llm_ms = round((time.perf_counter() - stage) * 1000)

    result["transcript"] = transcript
    result["timings_ms"] = {
        "stt": stt_ms,
        "llm": llm_ms,
        "total": round((time.perf_counter() - started) * 1000),
    }
    queries = []
    for done in result["searches"]:
        queries.append(done["query"])
    log.info("voice %r -> %s, searched %s, picked %d + %d (%dms)", transcript, result["operations"], queries,
             len(result["picks"]["geoserver"]), len(result["picks"]["emodnet"]), result["timings_ms"]["total"])
    return result

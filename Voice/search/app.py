"""AugGIS layer search.

Holds the list of every layer on the team's GeoServer and on EMODnet, and finds the ones a
few search words are about.

    POST /search   {"query": "wind farms baltic", "per_server": 10}

    200 -> {
      "query": "wind farms baltic",
      "version": "e9c62ca3ba54",
      "nothing_close": false,
      "results": {
        "geoserver": [{"id": "G129", "source": "geoserver", "group": "Baltic Sea",
                       "name": "balticline:BS_Wind_Farms", "spoken": "Wind Farms",
                       "meaning": 0.81, "words": 0.92}, ...],
        "emodnet": [...]
      }
    }

    POST /layers   {"ids": ["G129", "E4"]}      the catalogue entries for these ids
    GET  /health   whether the list is loaded and prepared, and what each server gave

The list is fetched and prepared once, when the service starts, which takes about 18
seconds. Until then /search answers 503. Only the voice interface calls this service; it is
not published outside the compose network.
"""

import asyncio
import logging
import time
from contextlib import asynccontextmanager

import httpx
from fastapi import FastAPI
from fastapi.responses import JSONResponse
from pydantic import BaseModel

import catalogue
import matching

MAX_PER_SERVER = 25

logging.basicConfig(level=logging.INFO, format="%(asctime)s %(levelname)s %(message)s")
log = logging.getLogger("voice-search")


class State:
    """What the service has loaded. Filled by prepare(), read by the endpoints."""

    index: matching.Index | None = None
    by_id: dict[str, dict] = {}
    version: str = ""
    counts: dict = {}
    problem: str = ""


state = State()


async def prepare() -> None:
    """Fetch the layer list and work out every layer's vectors.

    The embedding model's container starts at the same time as this one and needs a few
    seconds, so a failure is tried again rather than ending the service.
    """
    while state.index is None:
        started = time.perf_counter()
        try:
            layers, counts = await catalogue.load_catalogue()
            if not layers:
                raise RuntimeError("no map server answered and no list is saved")

            index = matching.Index(layers)
            async with httpx.AsyncClient(timeout=180) as client:
                await index.prepare_meanings(client)
        except Exception as exc:
            state.problem = f"{type(exc).__name__}: {exc}"
            log.warning("not ready yet, trying again in 5 s: %s", state.problem)
            await asyncio.sleep(5)
            continue

        state.by_id = index.by_id
        state.version = catalogue.version_of(layers)
        state.counts = counts
        state.problem = ""
        state.index = index
        log.info("%d layers ready in %.1f s: %s", len(layers), time.perf_counter() - started, counts)


@asynccontextmanager
async def lifespan(app: FastAPI):
    """Start preparing in the background, so /health can answer while it runs."""
    task = asyncio.create_task(prepare())
    yield
    task.cancel()


app = FastAPI(title="AugGIS layer search", lifespan=lifespan)


class SearchRequest(BaseModel):
    query: str
    per_server: int = 10


class LayersRequest(BaseModel):
    ids: list[str]


def not_ready() -> JSONResponse:
    """The answer while the list is still being prepared, or could not be."""
    message = "the layer list is still being prepared"
    if state.problem:
        message = message + " (" + state.problem + ")"
    return JSONResponse({"error": message}, status_code=503)


@app.get("/health")
async def health():
    """Report whether the list is ready, how many layers each server gave, and its version."""
    return {
        "ready": state.index is not None,
        "layers": len(state.by_id),
        "servers": state.counts,
        "version": state.version,
        "problem": state.problem,
    }


@app.post("/search")
async def search(request: SearchRequest):
    """Find the layers on each server closest to a few search words."""
    if state.index is None:
        return not_ready()

    query = request.query.strip()
    if not query:
        return JSONResponse({"error": "query is empty"}, status_code=400)
    per_server = max(1, min(request.per_server, MAX_PER_SERVER))

    started = time.perf_counter()
    try:
        async with httpx.AsyncClient(timeout=60) as client:
            found = await state.index.search(client, query, per_server)
    except (httpx.HTTPError, KeyError) as exc:
        # KeyError: the embedding model answered, but without the vectors.
        log.exception("embedding model failed")
        return JSONResponse({"error": f"embedding model unavailable: {exc}"}, status_code=503)

    log.info("%r -> %d + %d layers (%d ms)", query, len(found["results"]["geoserver"]),
             len(found["results"]["emodnet"]), round((time.perf_counter() - started) * 1000))
    return {
        "query": query,
        "version": state.version,
        "nothing_close": found["nothing_close"],
        "results": found["results"],
    }


@app.post("/layers")
async def layers(request: LayersRequest):
    """Look layers up by id. Ids the list does not have are left out."""
    if state.index is None:
        return not_ready()

    found = []
    for layer_id in request.ids:
        if layer_id in state.by_id:
            found.append(state.by_id[layer_id])
    return {"version": state.version, "layers": found}

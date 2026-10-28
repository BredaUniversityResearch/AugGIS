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
                       "kind": "unknown", "meaning": 0.81, "words": 0.92}, ...],
        "emodnet": [...]
      }
    }

    POST /layers   {"ids": ["G129", "E4"]}      the catalogue entries for these ids

    POST /download {"source": "emodnet", "name": "windfarmspoly",
                    "region": [3797132, 3137432, 3965925, 3291676]}

    200 -> {"kind": "shapes", "title": "Wind Farms (Polygons)", "source": "emodnet",
            "name": "windfarmspoly", "region": [...], "geojson": <the shapes as the server sent them>,
            "features": 48, "matched": 48, "held_back": "", "timings_ms": {"shapes": 900, "total": 910}}

    A picture layer, a layer group, or any layer with "as_picture": true, comes back as the
    PNG its server draws: "kind": "picture", "width", "height", "bytes", "png_base64". The
    PNG is in the JSON as base64 so that one answer holds the whole layer and this service
    keeps nothing.

    A team GeoServer layer ("source": "geoserver", "name": "northsee:NS_Pipelines") is asked
    what it is first (one DescribeLayer) and then downloaded the same way, with the login
    from GEOSERVER_USER / GEOSERVER_PASSWORD; its answer also has "served_as" ("WFS", "WCS",
    "WMS" or "group of 4").

    422 -> {"error": "Nothing from Route Density (EMSA) ... lies in this region: its picture is empty. ..."}

    GET  /health   whether the list is loaded and prepared, and what each server gave

The list is fetched and prepared once, when the service starts, which takes about 18
seconds. Until then /search answers 503. The voice interface calls this service and passes
/search and /download on from its own published address; this service is not published
outside the compose network. A download is fetched fresh each time and nothing is kept.
"""

import asyncio
import logging
import math
import time
from contextlib import asynccontextmanager

import httpx
from fastapi import FastAPI
from fastapi.responses import JSONResponse
from pydantic import BaseModel

import area
import catalogue
import download
import geoserver
import matching
import picture

MAX_PER_SERVER = 25

logging.basicConfig(level=logging.INFO, format="%(asctime)s %(levelname)s %(message)s")
log = logging.getLogger("voice-search")


class State:
    """What the service has loaded. Filled by prepare(), read by the endpoints."""

    index: matching.Index | None = None
    by_id: dict[str, dict] = {}
    # Keyed by (source, name), the way a download names its layer.
    by_name: dict[tuple[str, str], dict] = {}
    version: str = ""
    counts: dict = {}
    problem: str = ""


state = State()


def layers_by_name(layers: list[dict]) -> dict[tuple[str, str], dict]:
    """Index the layers by (source, name).

    The same name can be listed twice, such as a layer inside a group and on its own. The
    entry kept is the first one, unless a later one can be downloaded as shapes.
    """
    by_name = {}
    for layer in layers:
        key = (layer["source"], layer["name"])
        if key not in by_name:
            by_name[key] = layer
        elif layer["kind"] == "shapes" and by_name[key]["kind"] != "shapes":
            by_name[key] = layer
    return by_name


def kind_counts(layers) -> dict[str, int]:
    """How many layers there are of each kind: {"shapes": 380, "picture": 70, ...}."""
    counts: dict[str, int] = {}
    for layer in layers:
        counts[layer["kind"]] = counts.get(layer["kind"], 0) + 1
    return counts


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
        state.by_name = layers_by_name(layers)
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
    # The map's [minx, miny, maxx, maxy] in EPSG:3035. With it, each result gets "on_map".
    region: list[float] | None = None


class LayersRequest(BaseModel):
    ids: list[str]


class DownloadRequest(BaseModel):
    source: str = "emodnet"
    name: str
    # [minx, miny, maxx, maxy] in EPSG:3035. Left out, it is download.DEFAULT_REGION.
    region: list[float] | None = None
    # Download the layer as the server draws it (a PNG) even when it has shapes.
    as_picture: bool = False


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
        "kinds": kind_counts(state.by_id.values()),
        "version": state.version,
        "problem": state.problem,
        "geoserver_login": geoserver.login() is not None,
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

    if request.region is not None:
        problem = region_problem(request.region)
        if problem:
            return JSONResponse({"error": problem}, status_code=400)
        region_box = area.region_in_degrees(request.region)
        for source in ("geoserver", "emodnet"):
            for layer in found["results"][source]:
                layer["on_map"] = area.on_map(layer["box"], region_box)

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


def region_problem(region: list[float]) -> str:
    """What is wrong with a region, or "" when it is 4 numbers with the corners the right way round."""
    if len(region) != 4:
        return "region must be 4 numbers: minx, miny, maxx, maxy in EPSG:3035"
    for value in region:
        if not math.isfinite(value):
            return "region must be 4 finite numbers: minx, miny, maxx, maxy in EPSG:3035, not NaN or infinity"
    if region[0] >= region[2] or region[1] >= region[3]:
        return "region must be minx, miny, maxx, maxy, with each min smaller than its max"
    return ""


@app.post("/download")
async def download_layer(request: DownloadRequest):
    """Download one layer from the list for a region, as the file its server sends."""
    if state.index is None:
        return not_ready()

    region = download.DEFAULT_REGION
    if request.region is not None:
        region = request.region
    problem = region_problem(region)
    if problem:
        return JSONResponse({"error": problem}, status_code=400)

    key = (request.source, request.name)
    if key not in state.by_name:
        message = "no layer called " + repr(request.name) + " from " + request.source + " in the layer list"
        return JSONResponse({"error": message}, status_code=404)
    layer = state.by_name[key]

    # Only the server's own box is trusted for a refusal; a sea's box is drawn by hand.
    if layer["box_from"] == "layer":
        if area.on_map(layer["box"], area.region_in_degrees(region)) == "no":
            message = (layer["spoken"] + " covers another area (" + area.box_words(layer["box"])
                       + "), so nothing of it lies on this map.")
            log.info("download %s:%s refused before fetching (422): %s", request.source, request.name, message)
            return JSONResponse({"error": message}, status_code=422)

    is_picture = request.as_picture or layer["kind"] == "picture"
    # A group comes as the picture its server draws of all its members.
    is_group = layer["kind"] == "group"
    try:
        if layer["source"] == "geoserver":
            result = await geoserver.download_layer(layer, region, request.as_picture)
        elif is_picture or is_group:
            result = await picture.download_picture(layer, region)
        else:
            result = await download.download(layer, region)
    except download.Refused as refused:
        log.info("download %s:%s refused (%d): %s", request.source, request.name, refused.status, refused.message)
        return JSONResponse({"error": refused.message}, status_code=refused.status)

    if result["kind"] == "picture":
        log.info("download %s:%s -> picture %dx%d, %d bytes (%d ms)", request.source, request.name,
                 result["width"], result["height"], result["bytes"], result["timings_ms"]["total"])
    else:
        log.info("download %s:%s -> %d shapes (%d ms)", request.source, request.name, result["features"],
                 result["timings_ms"]["total"])
        if result["held_back"]:
            log.warning("download %s:%s held back: %s", request.source, request.name, result["held_back"])

    answer = {"kind": result["kind"], "source": layer["source"], "name": layer["name"], "region": region}
    answer.update(result)
    return answer

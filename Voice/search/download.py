"""Download one EMODnet shape layer for a region, as the GeoJSON its server sends.

The team GeoServer's layers go through geoserver.py, which uses the same pieces with its
login. The shapes come from the layer's WFS address as GeoJSON in EPSG:3035, the projection an
AugGIS config uses. The GeoJSON is passed on as the server sent it; nothing in it is changed.

A layer that is not shapes is fetched as a picture by picture.py, which uses the pieces here.
"""

import html
import re
import time

import httpx

# The Belgian and southern Dutch coast, EPSG:3035, easting first: minx, miny, maxx, maxy.
DEFAULT_REGION = [3797132.0, 3137432.0, 3965925.0, 3291676.0]

# A whole layer can be many megabytes, so the read is allowed to take minutes.
TIMEOUT = httpx.Timeout(300, connect=20)

JAVA_EXCEPTION_NAME = re.compile(r"^[\w.]+Exception: ")
EXCEPTION_TEXT = re.compile(r"<(?:\w+:)?ExceptionText>(.*?)</(?:\w+:)?ExceptionText>", re.DOTALL)
# The way WMS 1.1.1 wraps its reason: <ServiceException code="LayerNotDefined">...</ServiceException>.
# The space or ">" after the tag name keeps the outer <ServiceExceptionReport> from matching.
SERVICE_EXCEPTION = re.compile(r"<ServiceException(?:\s[^>]*)?>(.*?)</ServiceException>", re.DOTALL)

# How each source is named at the start of a sentence on the panel.
SERVER_NAMES = {"emodnet": "EMODnet", "geoserver": "The team GeoServer"}

LOGIN_REFUSED = ("The team GeoServer refused the helper's login (401). Check GEOSERVER_USER and "
                 + "GEOSERVER_PASSWORD in Voice/.env, then recreate the search container.")


class Refused(Exception):
    """A layer that cannot be downloaded, with the HTTP status and a sentence saying why."""

    def __init__(self, status: int, message: str):
        super().__init__(message)
        self.status = status
        self.message = message


def server_name(layer: dict) -> str:
    """The layer's server as the start of a sentence: "EMODnet" or "The team GeoServer"."""
    return SERVER_NAMES[layer["source"]]


def held_back_sentence(title: str, matched, sent: int) -> str:
    """A sentence when the server has more shapes in the region than it sent, or "" when it sent them all.

    A WFS answer says how many shapes matched ("numberMatched"). A server set to send at most
    so many shapes per answer sends fewer, and the rest would be missing from the layer.
    "unknown" or a missing number says nothing, so it gives "".
    """
    if isinstance(matched, bool) or not isinstance(matched, int):
        return ""
    if matched <= sent:
        return ""
    return (title + ": the server has " + str(matched) + " shapes in this region but sent only " + str(sent)
            + ", so " + str(matched - sent) + " are missing from the layer.")


def features_of(collection: dict) -> list:
    """The shapes of a GeoJSON FeatureCollection, or [] when it holds none."""
    features = collection.get("features")
    if not isinstance(features, list):
        return []
    return features


def server_reason(body: str) -> str:
    """The reason in a GeoServer error, as ": <reason>.", or "." when it gave none.

    GeoServer wraps its reason in XML (<ows:ExceptionText>...</ows:ExceptionText>); only the
    reason is kept, so the answer reads as a sentence on the panel.
    """
    found = EXCEPTION_TEXT.search(body)
    if found is None:
        found = SERVICE_EXCEPTION.search(body)
    if found is None:
        return "."
    # The reason is XML text, so "&amp;" in it is "&".
    reason = html.unescape(" ".join(found.group(1).split()))
    reason = JAVA_EXCEPTION_NAME.sub("", reason)
    return ": " + reason[:200] + "."


async def get_shapes(client: httpx.AsyncClient, layer: dict, title: str, region: list[float]) -> dict:
    """Ask a layer's WFS for every shape that touches the region, as GeoJSON in EPSG:3035.

    The region's projection is written "EPSG:3035", which GeoServer reads easting first.
    The "urn:ogc:def:crs:EPSG::3035" spelling would swap the two numbers of every point.
    The same request works on EMODnet and on the team GeoServer (checked 2026-10-27).

    Raises:
        Refused: 504 when the server is too slow, 502 when it cannot be reached, refuses the
            login or sends no GeoJSON.
    """
    bbox = ",".join(format(value, ".2f") for value in region) + ",EPSG:3035"
    params = {
        "service": "WFS",
        "version": "2.0.0",
        "request": "GetFeature",
        "typeNames": layer["wfs_name"],
        "outputFormat": "application/json",
        "srsName": "EPSG:3035",
        "bbox": bbox,
    }
    server = server_name(layer)
    try:
        response = await client.get(layer["wfs_url"], params=params, headers={"User-Agent": "auggis-voice"})
    except httpx.TimeoutException:
        raise Refused(504, server + " did not send " + title + " in time; try again in a minute.")
    except httpx.HTTPError as exc:
        raise Refused(502, server + " could not be reached for " + title + " (" + type(exc).__name__ + ").")

    if response.status_code == 401:
        raise Refused(502, LOGIN_REFUSED)
    if response.status_code != 200:
        raise Refused(502, server + " answered " + str(response.status_code) + " for " + title
                      + server_reason(response.text))
    try:
        collection = response.json()
    except ValueError:
        raise Refused(502, server + " sent something other than shapes for " + title + server_reason(response.text))
    if not isinstance(collection, dict):
        raise Refused(502, server + " sent something other than shapes for " + title + ".")
    return collection


def refuse_geoserver(layer: dict, title: str) -> None:
    """Refuse a team GeoServer layer here: it needs the login, so it goes through geoserver.download_layer."""
    if layer["source"] != "emodnet":
        raise Refused(422, title + " is on the team GeoServer, which geoserver.download_layer downloads.")


async def download(layer: dict, region: list[float]) -> dict:
    """Download one catalogue layer's shapes for a region, as the GeoJSON its server sends.

    Nothing is kept: every call asks EMODnet again.

    Args:
        layer: The layer's catalogue entry.
        region: [minx, miny, maxx, maxy] in EPSG:3035.

    Returns:
        download_shapes's answer.

    Raises:
        Refused: For a layer that is not shapes, a region with nothing in it, or EMODnet failing.
            A picture goes to picture.download_picture instead.
    """
    started = time.perf_counter()
    title = layer["spoken"]

    refuse_geoserver(layer, title)
    kind = layer.get("kind", "unknown")
    if kind == "unknown":
        raise Refused(422, title + ": its server did not list its shapes when the layer list was loaded, "
                      + "so it cannot be downloaded until the search service restarts.")
    if kind != "shapes":
        raise Refused(422, title + " is a " + kind + ", not a shape layer.")

    async with httpx.AsyncClient(timeout=TIMEOUT, follow_redirects=True) as client:
        return await download_shapes(client, layer, title, region, started)


async def download_shapes(client: httpx.AsyncClient, layer: dict, title: str, region: list[float],
                          started: float) -> dict:
    """Download a shape layer's GeoJSON for the region.

    Args:
        client: An open HTTP client; for the team GeoServer it carries the login.
        layer: Where the layer is: "source", "wfs_name" and "wfs_url".
        title: The layer's spoken name.
        region: [minx, miny, maxx, maxy] in EPSG:3035.
        started: When the whole download started (time.perf_counter), for the total time.

    Returns:
        {"kind": "shapes", "title", "geojson": the FeatureCollection as the server sent it,
         "features": how many shapes it holds, "matched", "held_back" (a sentence when the
         server sent fewer shapes than it matched, else "") and "timings_ms"}

    Raises:
        Refused: 422 when nothing of the layer lies in the region.
    """
    asked = time.perf_counter()
    collection = await get_shapes(client, layer, title, region)
    shapes_ms = round((time.perf_counter() - asked) * 1000)

    features = features_of(collection)
    if not features:
        raise Refused(422, "Nothing from " + title + " lies in this region.")

    # GeoServer can be set to send at most so many shapes; held_back says when it did.
    matched = collection.get("numberMatched")
    return {
        "kind": "shapes",
        "title": title,
        "geojson": collection,
        "features": len(features),
        "matched": matched,
        "held_back": held_back_sentence(title, matched, len(features)),
        "timings_ms": {
            "shapes": shapes_ms,
            "total": round((time.perf_counter() - started) * 1000),
        },
    }

"""Download an EMODnet layer for a region as a picture, the PNG its server draws.

Some EMODnet layers come only as pictures (WMS), such as the route and vessel densities and
the bathymetry: the server draws them, and there are no shapes behind them. A shape layer or
a group can be asked as a picture too, which keeps the server's own colours where the shape
download can only give one colour per rule or per member.

The picture is one WMS GetMap of exactly the region, in EPSG:3035. WMS 1.1.1 is used because
it reads the box easting first, the way the region is written; 1.3.0 reads it northing first
for this projection and silently sends a picture of the wrong area.

A team GeoServer picture goes through geoserver.py, which calls fetch_picture with a client
that carries the login.
"""

import base64
import io
import time

import httpx
from PIL import Image

import download
from download import Refused

# The long edge of the picture in pixels. The short edge follows the region's shape.
LONG_EDGE = 1024


def picture_size(region: list[float]) -> tuple[int, int]:
    """The picture's width and height: LONG_EDGE on the region's longer side, the other in proportion."""
    width = region[2] - region[0]
    height = region[3] - region[1]
    if width >= height:
        return LONG_EDGE, max(1, round(LONG_EDGE * height / width))
    return max(1, round(LONG_EDGE * width / height)), LONG_EDGE


def is_empty(png: bytes) -> bool:
    """Whether every pixel of a picture is fully see-through, which is how WMS draws "nothing here"."""
    with Image.open(io.BytesIO(png)) as image:
        alpha = image.convert("RGBA").getchannel("A")
        highest = alpha.getextrema()[1]
    return highest == 0


async def get_picture(client: httpx.AsyncClient, layer: dict, title: str, region: list[float],
                      width: int, height: int) -> bytes:
    """Ask a layer's WMS to draw it over exactly the region, as a see-through PNG.

    Raises:
        Refused: 504 when the server is too slow, 502 when it cannot be reached, refuses the
            login or sends no PNG.
    """
    params = {
        "service": "WMS",
        "version": "1.1.1",
        "request": "GetMap",
        "layers": layer["name"],
        "styles": "",
        "srs": "EPSG:3035",
        "bbox": ",".join(format(value, ".2f") for value in region),
        "width": str(width),
        "height": str(height),
        "format": "image/png",
        "transparent": "true",
    }
    server = download.server_name(layer)
    try:
        response = await client.get(layer["url"], params=params, headers={"User-Agent": "auggis-voice"})
    except httpx.TimeoutException:
        raise Refused(504, server + " did not send the picture of " + title + " in time; try again in a minute.")
    except httpx.HTTPError as exc:
        raise Refused(502, server + " could not be reached for " + title + " (" + type(exc).__name__ + ").")

    content_type = response.headers.get("content-type", "")
    if response.status_code == 401:
        raise Refused(502, download.LOGIN_REFUSED)
    if response.status_code != 200:
        raise Refused(502, server + " answered " + str(response.status_code) + " for the picture of " + title
                      + download.server_reason(response.text))
    if not content_type.startswith("image/png"):
        raise Refused(502, server + " sent something other than a picture for " + title
                      + download.server_reason(response.text))
    return response.content


async def download_picture(layer: dict, region: list[float]) -> dict:
    """Download one catalogue layer for a region as the PNG its server draws.

    The PNG goes back inside the JSON answer as base64 text, so one answer holds the whole
    layer and the helper keeps nothing between calls.

    Args:
        layer: The layer's catalogue entry, of any kind.
        region: [minx, miny, maxx, maxy] in EPSG:3035.

    Returns:
        {"kind": "picture", "title", "width", "height", "bytes", "png_base64", "timings_ms"}

    Raises:
        Refused: For a team GeoServer layer, a picture with nothing on it, or EMODnet failing.
    """
    started = time.perf_counter()
    title = layer["spoken"]
    download.refuse_geoserver(layer, title)

    async with httpx.AsyncClient(timeout=download.TIMEOUT, follow_redirects=True) as client:
        return await fetch_picture(client, layer, title, region, started)


async def fetch_picture(client: httpx.AsyncClient, layer: dict, title: str, region: list[float],
                        started: float) -> dict:
    """Ask for a layer's picture of the region, check it is not empty, and build its answer.

    Args:
        client: An open HTTP client; for the team GeoServer it carries the login.
        layer: Where the layer is: "source", "name" and "url" (its WMS).
        title: The layer's spoken name.
        region: [minx, miny, maxx, maxy] in EPSG:3035.
        started: When the whole download started (time.perf_counter), for the total time.

    Returns:
        download_picture's answer.
    """
    width, height = picture_size(region)
    asked = time.perf_counter()
    png = await get_picture(client, layer, title, region, width, height)
    picture_ms = round((time.perf_counter() - asked) * 1000)

    checking = time.perf_counter()
    try:
        empty = is_empty(png)
    except (OSError, ValueError):
        raise Refused(502, download.server_name(layer) + " sent a picture of " + title + " that cannot be read.")
    # A layer the server draws only closer in, such as EUSeaMap's EUNIS 2019 map (shapes in
    # this region, but a scale limit of 1:500 000), also comes back fully see-through.
    if empty:
        raise Refused(422, "Nothing from " + title + " lies in this region: its picture is empty. "
                      + "(A layer the server draws only when zoomed in further comes back empty too.)")

    return {
        "kind": "picture",
        "title": title,
        "width": width,
        "height": height,
        "bytes": len(png),
        "png_base64": base64.b64encode(png).decode("ascii"),
        "timings_ms": {
            "picture": picture_ms,
            "check": round((time.perf_counter() - checking) * 1000),
            "total": round((time.perf_counter() - started) * 1000),
        },
    }

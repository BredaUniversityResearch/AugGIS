"""Download one layer of the team's GeoServer for a region, as the file it sends.

The layer list names the GeoServer's 494 layers from its tile-cache list, which does not say
what each one is, so they are all "unknown" there. Asking all 494 at start would be 494
requests; instead the one layer picked is asked at download time, with one WMS DescribeLayer:

    {"layerDescriptions": [{"layerName": "northsee:NS_Pipelines", "owsType": "WFS",
                            "typeName": "northsee:NS_Pipelines", ...}]}

That answer decides what is downloaded, the way MSP's GeoServerCommunicator does it:

    one description, owsType "WFS"      shapes, from WFS GetFeature (download.py)
    one description, "WCS" or none      a picture, from WMS GetMap (picture.py). WCS is a raster
                                        of values; no owsType is a layer the GeoServer passes on
                                        from another map server, such as EMODnet's vessel density
    several descriptions                a group, which comes as the picture the GeoServer draws
                                        of all its members (picture.py)

Every request carries the read-only login MSP itself uses, as HTTP basic authentication,
from GEOSERVER_USER and GEOSERVER_PASSWORD. Without it the GeoServer answers "no such layer"
for almost every layer, so without it nothing is asked and the answer says the login is not
set. The login is only ever sent to the GeoServer's own address: the addresses inside a
DescribeLayer answer are not used.
"""

import os
import re
import time

import httpx

import download
import picture
from download import Refused

BASE = "https://geo.mspchallenge.info/geoserver"
OWS = BASE + "/ows"
WMS = BASE + "/wms"
WFS = BASE + "/wfs"

NO_LOGIN = ("The team GeoServer login is not set on the helper, so its layers cannot be downloaded. "
            + "Put GEOSERVER_USER and GEOSERVER_PASSWORD in Voice/.env and recreate the search container.")

# GeoServer's answer for a layer it does not have, or that this login may not read.
NO_SUCH_LAYER = re.compile(r"code=\"LayerNotDefined\"")


def login() -> tuple[str, str] | None:
    """The GeoServer login from the environment, or None when either half is missing."""
    user = os.getenv("GEOSERVER_USER", "").strip()
    password = os.getenv("GEOSERVER_PASSWORD", "")
    if not user or not password:
        return None
    return user, password


def make_client(auth: tuple[str, str]) -> httpx.AsyncClient:
    """An HTTP client that sends the login with every request.

    httpx leaves the login off when a redirect goes to another host.
    """
    return httpx.AsyncClient(timeout=download.TIMEOUT, follow_redirects=True, auth=auth)


async def describe(client: httpx.AsyncClient, layer: dict, title: str) -> list[dict]:
    """Ask the GeoServer what a layer is: its DescribeLayer answer's layerDescriptions.

    Raises:
        Refused: 404 when the GeoServer says it has no such layer, 502 when it refuses the
            login, cannot be reached or answers with something else, 504 when it is too slow.
    """
    params = {
        "service": "WMS",
        "version": "1.1.1",
        "request": "DescribeLayer",
        "layers": layer["name"],
        "outputFormat": "application/json",
    }
    try:
        response = await client.get(OWS, params=params, headers={"User-Agent": "auggis-voice"})
    except httpx.TimeoutException:
        raise Refused(504, "The team GeoServer did not say what " + title + " is in time; try again in a minute.")
    except httpx.HTTPError as exc:
        raise Refused(502, "The team GeoServer could not be reached for " + title + " (" + type(exc).__name__ + ").")

    if response.status_code == 401:
        raise Refused(502, download.LOGIN_REFUSED)
    if response.status_code != 200:
        raise Refused(502, "The team GeoServer answered " + str(response.status_code) + " when asked what "
                      + title + " is" + download.server_reason(response.text))
    if NO_SUCH_LAYER.search(response.text):
        raise Refused(404, "The team GeoServer says it has no layer " + layer["name"] + " (" + title + "). "
                      + "Either the helper's read-only login may not read it, or it was deleted and only "
                      + "its entry in the tile-cache list is left.")
    try:
        descriptions = response.json().get("layerDescriptions")
    except (ValueError, AttributeError):
        raise Refused(502, "The team GeoServer could not say what " + title + " is"
                      + download.server_reason(response.text))
    if not isinstance(descriptions, list) or not descriptions:
        raise Refused(502, "The team GeoServer described " + title + " with no layers in it.")
    return descriptions


def kind_of(descriptions: list[dict]) -> str:
    """What a DescribeLayer answer makes a layer: "group", "shapes" or "picture"."""
    if len(descriptions) > 1:
        return "group"
    if descriptions[0].get("owsType") == "WFS":
        return "shapes"
    return "picture"


def served_as(descriptions: list[dict]) -> str:
    """How the GeoServer described a layer, for the answer: "WFS", "WCS", "WMS" or "group of 4"."""
    if len(descriptions) > 1:
        return "group of " + str(len(descriptions))
    ows_type = descriptions[0].get("owsType")
    if not ows_type:
        return "WMS"
    return str(ows_type)


def type_name_of(description: dict, fallback: str) -> str:
    """The WFS name in one description, which can differ from the layer's own name."""
    type_name = description.get("typeName")
    if not type_name:
        type_name = description.get("layerName")
    if not type_name:
        type_name = fallback
    return str(type_name)


def shape_layer(name: str, type_name: str) -> dict:
    """Where a GeoServer layer's shapes are, in the form download.py reads."""
    return {"source": "geoserver", "name": name, "wfs_url": WFS, "wfs_name": type_name}


def picture_layer(layer: dict) -> dict:
    """Where a GeoServer layer's picture is, in the form picture.py reads."""
    return {"source": "geoserver", "name": layer["name"], "url": WMS}


async def download_layer(layer: dict, region: list[float], as_picture: bool) -> dict:
    """Download one team GeoServer layer for a region, as the file it sends.

    Nothing is kept: every call asks the GeoServer again, starting with its DescribeLayer.

    Args:
        layer: The layer's catalogue entry ("source" "geoserver", "name", "spoken").
        region: [minx, miny, maxx, maxy] in EPSG:3035.
        as_picture: Download it as the GeoServer draws it (a PNG), even when it has shapes.

    Returns:
        The same answer as an EMODnet download of that kind ("shapes" or "picture"), plus
        "served_as" (how the GeoServer described it) and the DescribeLayer time in "timings_ms".

    Raises:
        Refused: With a sentence for a missing or refused login, a layer the GeoServer does
            not have, a region with nothing in it, or the GeoServer failing.
    """
    started = time.perf_counter()
    title = layer["spoken"]
    auth = login()
    if auth is None:
        raise Refused(503, NO_LOGIN)

    async with make_client(auth) as client:
        descriptions, describe_ms = await download.timed(describe(client, layer, title))
        kind = kind_of(descriptions)
        if as_picture or kind != "shapes":
            result = await picture.fetch_picture(client, picture_layer(layer), title, region, started)
        else:
            type_name = type_name_of(descriptions[0], layer["name"])
            result = await download.download_shapes(client, shape_layer(layer["name"], type_name), title,
                                                    region, started)

    result["served_as"] = served_as(descriptions)
    result["timings_ms"]["describe"] = describe_ms
    result["timings_ms"]["total"] = round((time.perf_counter() - started) * 1000)
    return result

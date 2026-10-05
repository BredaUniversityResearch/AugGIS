"""Every layer on the team's GeoServer and on EMODnet, as one list.

Each layer is a dict:

    {"id": "G1", "source": "geoserver", "group": "North Sea",
     "name": "northsee:NS_Wind_Farms_Implemented",
     "title": "Generated Layer - NS_Wind_Farms_Implemented",
     "spoken": "Wind Farms Implemented"}

`name` is what a map server wants back when the layer is requested. `spoken` is what a
person would say, and is the text that gets matched: most GeoServer titles are just the
machine name, which nobody says out loud.

No login is needed. The GeoServer list is its tile-cache list (WMTS), which is public and is
the only list that names all of its layers; its WMS, WFS and WCS lists show 6 of them.
"""

import asyncio
import hashlib
import json
import logging
import os
import re
import xml.etree.ElementTree as ElementTree
from pathlib import Path

import httpx

CACHE = Path(os.getenv("CATALOGUE_CACHE", "/data/catalogue.json"))

GEOSERVER_LIST = "https://geo.mspchallenge.info/geoserver/gwc/service/wmts?REQUEST=GetCapabilities"

# EMODnet is 7 map servers, one per theme, with no shared list.
EMODNET_SERVERS = {
    "Human activities": "https://ows.emodnet-humanactivities.eu/wms",
    "Bathymetry": "https://ows.emodnet-bathymetry.eu/wms",
    "Seabed habitats": "https://ows.emodnet-seabedhabitats.eu/geoserver/emodnet_view/wms",
    "Geology": "https://drive.emodnet-geology.eu/geoserver/wms",
    "Biology": "https://geo.vliz.be/geoserver/Emodnetbio/wms",
    "Physics": "https://geoserver.emodnet-physics.eu/geoserver/emodnet/wms",
    "Chemistry": "https://ows.emodnet-chemistry.eu/wms",
}

# GeoServer workspaces are sea regions, apart from GeoServer's own demo data.
REGIONS = {
    "northsee": "North Sea",
    "balticline": "Baltic Sea",
    "adriatic": "Adriatic Sea",
    "easternmed": "Eastern Mediterranean",
    "simcelt": "Celtic Sea",
    "mozambicanchannel": "Mozambique Channel",
    "caribbean": "Caribbean",
}

# Short region codes the MSP data puts in front of a layer name: NS_Pipelines, BS_Cables.
MACHINE_PREFIX = re.compile(r"^(NS|BS|WBS|AS|CR|EA|EMS|ems|NL|DE|BE|DK|UK)_")

log = logging.getLogger("voice-search")


def local_name(element: ElementTree.Element) -> str:
    """An element's tag without its namespace: "{http://www.opengis.net/wms}Layer" gives "Layer"."""
    return element.tag.split("}")[-1]


def child_text(element: ElementTree.Element, name: str) -> str | None:
    """The text of an element's direct child with this tag name, or None if it has none."""
    for child in element:
        if local_name(child) == name:
            return (child.text or "").strip()
    return None


async def fetch(client: httpx.AsyncClient, url: str, attempts: int = 3) -> ElementTree.Element | None:
    """Download a capabilities document and parse it, asking again if it has no layers.

    Two of the EMODnet servers sometimes answer with an empty list and are fine on the next
    try, so an answer without layers is treated like a failed one.

    Returns:
        The document's root element, or None if every attempt failed.
    """
    for _ in range(attempts):
        try:
            response = await client.get(url, headers={"User-Agent": "auggis-voice"})
            if response.status_code == 200 and "<Layer" in response.text:
                return ElementTree.fromstring(response.content)
            log.warning("%s: answered %d without a layer list", url, response.status_code)
        except (httpx.HTTPError, ElementTree.ParseError) as exc:
            log.warning("%s: %s", url, exc)
        await asyncio.sleep(1)
    return None


def speakable(name: str, title: str) -> str:
    """Turn a layer's title into what a person would say for it.

    141 of the 164 North Sea titles are "Generated Layer - NS_Wind_Farms_Implemented" or the
    bare machine name. Those become "Wind Farms Implemented". A title that is already
    written for people is kept as it is.

    Args:
        name: The layer's machine name, with or without its workspace.
        title: The title the server gives.
    """
    text = title.strip()
    if text.startswith("Generated Layer - "):
        text = text[len("Generated Layer - "):]
    if not text:
        text = name.split(":")[-1]

    has_spaces = " " in text
    has_underscores = "_" in text
    is_the_name = text == name.split(":")[-1]
    if has_spaces:
        return text
    if not has_underscores and not is_the_name:
        return text

    # A few titles carry the workspace too: "easternmed:EMS_Aquaculture_emodnet".
    text = text.split(":")[-1]
    text = MACHINE_PREFIX.sub("", text)
    text = text.replace("_", " ")
    # windFarms and WindFarms both become two words.
    text = re.sub(r"(?<=[a-z])(?=[A-Z])", " ", text)
    return re.sub(r"\s+", " ", text).strip()


async def geoserver_layers(client: httpx.AsyncClient) -> list[dict]:
    """List every layer the team's GeoServer names in its tile-cache list, without ids."""
    root = await fetch(client, GEOSERVER_LIST)
    if root is None:
        return []

    layers = []
    for element in root.iter():
        if local_name(element) != "Layer":
            continue
        name = child_text(element, "Identifier")
        if not name:
            continue
        title = child_text(element, "Title") or ""
        workspace = name.split(":")[0]
        layers.append({
            "source": "geoserver",
            "group": REGIONS.get(workspace, "GeoServer demo (" + workspace + ")"),
            "name": name,
            "title": title,
            "spoken": speakable(name, title),
        })
    return layers


async def emodnet_server_layers(client: httpx.AsyncClient, theme: str, url: str) -> list[dict]:
    """List the layers of one EMODnet map server, without ids.

    A WMS list nests layers inside folder layers. Only a layer with a name can be requested,
    so the folders, which have a title and no name, are left out.
    """
    root = await fetch(client, url + "?service=WMS&version=1.3.0&request=GetCapabilities")
    if root is None:
        return []

    layers = []
    for element in root.iter():
        if local_name(element) != "Layer":
            continue
        name = child_text(element, "Name")
        if not name:
            continue
        title = re.sub(r"\s+", " ", child_text(element, "Title") or "")
        layers.append({
            "source": "emodnet",
            "group": theme,
            "name": name,
            "url": url,
            "title": title,
            "spoken": speakable(name, title),
        })
    return layers


def number(geoserver: list[dict], emodnet: list[dict]) -> list[dict]:
    """Give the layers their ids, G0, G1 ... for GeoServer and E0, E1 ... for EMODnet."""
    for index, layer in enumerate(geoserver):
        layer["id"] = "G" + str(index)
    for index, layer in enumerate(emodnet):
        layer["id"] = "E" + str(index)
    return geoserver + emodnet


def saved_layers(source: str, group: str | None) -> list[dict]:
    """The layers of one server from the list saved last time, or none if nothing is saved.

    Args:
        source: "geoserver" or "emodnet".
        group: The EMODnet theme, which is one server. None takes every group.
    """
    if not CACHE.exists():
        return []

    layers = []
    for layer in json.loads(CACHE.read_text(encoding="utf-8")):
        if layer["source"] != source:
            continue
        if group is not None and layer["group"] != group:
            continue
        layers.append(layer)
    return layers


async def load_catalogue() -> tuple[list[dict], dict]:
    """Ask all 8 servers for their layers at once, and save the result.

    A server that does not answer keeps the layers it had in the list saved last time, so
    one server being down at start does not drop its layers from the search.

    Returns:
        The layers with their ids, and how each server's layers were got: a count, or
        "12 (did not answer, from the saved list)" when the saved ones were used.
    """
    async with httpx.AsyncClient(timeout=60, follow_redirects=True) as client:
        jobs = [geoserver_layers(client)]
        for theme, url in EMODNET_SERVERS.items():
            jobs.append(emodnet_server_layers(client, theme, url))
        answers = await asyncio.gather(*jobs)

    counts = {}

    geoserver = answers[0]
    counts["GeoServer"] = str(len(geoserver))
    if not geoserver:
        geoserver = saved_layers("geoserver", None)
        counts["GeoServer"] = str(len(geoserver)) + " (did not answer, from the saved list)"

    emodnet = []
    for theme, server_layers in zip(EMODNET_SERVERS, answers[1:]):
        counts["EMODnet " + theme] = str(len(server_layers))
        if not server_layers:
            server_layers = saved_layers("emodnet", theme)
            counts["EMODnet " + theme] = str(len(server_layers)) + " (did not answer, from the saved list)"
        emodnet.extend(server_layers)

    layers = number(geoserver, emodnet)
    if layers:
        CACHE.parent.mkdir(parents=True, exist_ok=True)
        CACHE.write_text(json.dumps(layers, indent=1), encoding="utf-8")
    return layers, counts


def version_of(layers: list[dict]) -> str:
    """A short fingerprint of the list.

    The ids are positions in the list, so they only mean the same layers while the list is
    the same. A caller that kept ids compares this to know whether they still hold.
    """
    names = []
    for layer in layers:
        names.append(layer["id"] + "=" + layer["source"] + ":" + layer["name"])
    return hashlib.sha1("\n".join(names).encode("utf-8")).hexdigest()[:12]

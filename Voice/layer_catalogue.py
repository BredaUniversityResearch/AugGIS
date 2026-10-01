"""Every layer on the team's GeoServer and on EMODnet, as one list.

    from layer_catalogue import load_catalogue
    layers = load_catalogue()          # fetched live, cached to _catalogue.json

Each layer is a dict:

    {"id": "G12", "source": "geoserver", "group": "North Sea",
     "name": "northsee:NS_Wind_Farms_Implemented",
     "title": "Generated Layer - NS_Wind_Farms_Implemented",
     "spoken": "Wind Farms Implemented"}

`name` is what a map server wants back when the layer is downloaded. `spoken` is what a
person would say, and is the text that gets matched: most GeoServer titles are just the
machine name, which nobody says out loud.

No login is needed. The GeoServer list is its tile-cache list (WMTS), which is public and is
the only list that names all of its layers. See HOW-TO-LIST-ALL-LAYERS.md beside the
GeoServer recording for why the WMS, WFS and WCS lists are not used.
"""

import html
import json
import re
import time
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path

import requests

HERE = Path(__file__).parent
CACHE = HERE / "_catalogue.json"

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


def fetch(url: str, attempts: int = 3) -> str:
    """Download a capabilities document, asking again if the answer is empty.

    Two of the EMODnet servers sometimes answer with an empty list and are fine on the
    next try, so an empty answer is treated like a failed one.

    Args:
        url: The address to ask.
        attempts: How many times to ask before giving up.

    Returns:
        The response body, or "" if every attempt failed.
    """
    for _ in range(attempts):
        try:
            response = requests.get(url, timeout=60, headers={"User-Agent": "auggis-voice"})
            if response.status_code == 200 and "<Layer" in response.text:
                return response.text
        except requests.RequestException:
            pass
        time.sleep(1)
    return ""


def speakable(name: str, title: str) -> str:
    """Turn a layer's title into what a person would say for it.

    141 of the 164 North Sea titles are "Generated Layer - NS_Wind_Farms_Implemented" or the
    bare machine name. Those become "Wind Farms Implemented". A title that is already
    written for people is kept as it is.

    Args:
        name: The layer's machine name, with or without its workspace.
        title: The title the server gives.

    Returns:
        The text to show and to match against.
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


def geoserver_layers() -> list[dict]:
    """List every layer the team's GeoServer names in its tile-cache list.

    Returns:
        One dict per layer, without ids. Empty if the server did not answer.
    """
    text = fetch(GEOSERVER_LIST)
    found = re.findall(
        r"<Layer>\s*<ows:Title>([^<]*)</ows:Title>.*?<ows:Identifier>([^<]+)</ows:Identifier>",
        text, flags=re.S)

    layers = []
    for title, name in found:
        title = html.unescape(title)
        workspace = name.split(":")[0]
        layers.append({
            "source": "geoserver",
            "group": REGIONS.get(workspace, "GeoServer demo (" + workspace + ")"),
            "name": name,
            "title": title,
            "spoken": speakable(name, title),
        })
    return layers


def emodnet_server_layers(theme: str, url: str) -> list[dict]:
    """List the layers of one EMODnet map server.

    Args:
        theme: The server's theme, used as the group.
        url: The server's WMS address.

    Returns:
        One dict per layer, without ids. Empty if the server did not answer.
    """
    text = fetch(url + "?service=WMS&version=1.3.0&request=GetCapabilities")
    found = re.findall(r"<Layer[^>]*>\s*<Name>([^<]+)</Name>\s*<Title>([^<]*)</Title>", text)

    layers = []
    for name, title in found:
        title = html.unescape(re.sub(r"\s+", " ", title).strip())
        layers.append({
            "source": "emodnet",
            "group": theme,
            "name": name,
            "url": url,
            "title": title,
            "spoken": speakable(name, title),
        })
    return layers


def fetch_catalogue() -> tuple[list[dict], dict]:
    """Ask all 8 servers for their layers at once.

    Returns:
        The layers, with ids G0, G1 ... for GeoServer and E0, E1 ... for EMODnet, and a
        count per server so a server that did not answer can be reported.
    """
    with ThreadPoolExecutor(max_workers=8) as pool:
        geoserver_job = pool.submit(geoserver_layers)
        emodnet_jobs = {}
        for theme, url in EMODNET_SERVERS.items():
            emodnet_jobs[theme] = pool.submit(emodnet_server_layers, theme, url)

        geoserver = geoserver_job.result()
        counts = {"GeoServer": len(geoserver)}
        emodnet = []
        for theme, job in emodnet_jobs.items():
            server_layers = job.result()
            counts["EMODnet " + theme] = len(server_layers)
            emodnet.extend(server_layers)

    for index, layer in enumerate(geoserver):
        layer["id"] = "G" + str(index)
    for index, layer in enumerate(emodnet):
        layer["id"] = "E" + str(index)
    return geoserver + emodnet, counts


def load_catalogue(refresh: bool = True) -> tuple[list[dict], dict]:
    """Fetch the catalogue, or read the last one saved if the servers cannot be reached.

    Args:
        refresh: False reads _catalogue.json without asking the servers.

    Returns:
        The layers and the count per server. The counts say "cached" when the list came
        from the file.
    """
    if refresh:
        layers, counts = fetch_catalogue()
        if layers:
            CACHE.write_text(json.dumps(layers, indent=1), encoding="utf-8")
            return layers, counts

    if not CACHE.exists():
        raise SystemExit("could not reach the map servers and there is no _catalogue.json yet")
    layers = json.loads(CACHE.read_text(encoding="utf-8"))
    return layers, {"cached": len(layers)}

"""Every layer on EMODnet, as one list.

    from layer_catalogue import load_catalogue
    layers = load_catalogue()          # fetched live, cached to _catalogue.json

Each layer is a dict:

    {"id": "E205", "source": "emodnet", "group": "Human activities",
     "name": "windfarms", "url": "https://ows.emodnet-humanactivities.eu/wms",
     "title": "Wind Farms (Points)", "spoken": "Wind Farms (Points)"}

`name` is what the map server wants back when the layer is downloaded. `spoken` is what a
person would say, and is the text that gets matched. No login is needed.
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
            "spoken": title,
        })
    return layers


def fetch_catalogue() -> tuple[list[dict], dict]:
    """Ask all 7 servers for their layers at once.

    Returns:
        The layers, with ids E0, E1 ..., and a count per server so a server that did not
        answer can be reported.
    """
    with ThreadPoolExecutor(max_workers=7) as pool:
        jobs = {}
        for theme, url in EMODNET_SERVERS.items():
            jobs[theme] = pool.submit(emodnet_server_layers, theme, url)

        counts = {}
        layers = []
        for theme, job in jobs.items():
            server_layers = job.result()
            counts["EMODnet " + theme] = len(server_layers)
            layers.extend(server_layers)

    for index, layer in enumerate(layers):
        layer["id"] = "E" + str(index)
    return layers, counts


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

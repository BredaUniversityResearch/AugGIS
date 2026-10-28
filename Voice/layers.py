"""Search the online layer list, and download one layer as the file its server sends.

    python layers.py search "wind farms"
    python layers.py download "wind farms" --pick e3
    python layers.py download --name windfarmspoly --out windfarms.geojson
    python layers.py download --name powercables --out cables.png
    python layers.py download --name windfarmspoly --as-picture --out windfarms.png
    python layers.py download "wind farms implemented" --pick g1 --out geoserver-windfarms.geojson
    python layers.py download --source geoserver --name northsee:NS_Pipelines

search prints the closest layers on each server, numbered g1, g2 ... for the team GeoServer
and e1, e2 ... for EMODnet. download runs the same search and downloads the one picked
(--pick 3 means e3), or the layer with that catalogue name (--name, on EMODnet unless
--source geoserver). A team GeoServer layer shows as "unknown" in the search; what it is
(shapes, a group or a picture) is asked when it is downloaded, and printed as "served as".

A shape layer comes back as the GeoJSON its server sends, and a picture layer, a layer
group (or any layer with --as-picture) as the PNG its server draws.

--out writes that file: the GeoJSON, or the PNG. Needs only Python itself and the voice
stack running (docker compose -f voice-stack.yml up -d).
"""

import argparse
import base64
import json
import math
import sys
import urllib.error
import urllib.request
from pathlib import Path

DEFAULT_HELPER = "http://localhost:8080"

# The Belgian and southern Dutch coast, EPSG:3035, easting first.
DEFAULT_REGION = "3797132,3137432,3965925,3291676"

SERVERS = (("geoserver", "g", "Team GeoServer"), ("emodnet", "e", "EMODnet"))


class HelperError(Exception):
    """The helper answered with an error, or did not answer."""


def post(helper: str, path: str, body: dict) -> dict:
    """Send a JSON request to the helper and return its JSON answer.

    Raises:
        HelperError: With the helper's own "error" sentence when it refused.
    """
    request = urllib.request.Request(
        helper.rstrip("/") + path,
        data=json.dumps(body).encode("utf-8"),
        headers={"Content-Type": "application/json"},
        method="POST",
    )
    try:
        with urllib.request.urlopen(request, timeout=360) as response:
            return json.loads(response.read())
    except urllib.error.HTTPError as error:
        text = error.read().decode("utf-8", "replace")
        try:
            message = json.loads(text)["error"]
        except (ValueError, KeyError, TypeError):
            message = text[:300]
        raise HelperError(str(error.code) + ": " + str(message))
    except urllib.error.URLError as error:
        raise HelperError("the helper at " + helper + " did not answer (" + str(error.reason)
                          + "). Is the voice stack running?")


def parse_region(text: str) -> list[float]:
    """"minx,miny,maxx,maxy" as 4 numbers."""
    parts = text.split(",")
    if len(parts) != 4:
        raise argparse.ArgumentTypeError("region must be minx,miny,maxx,maxy")
    numbers = []
    for part in parts:
        number = float(part)
        if not math.isfinite(number):
            raise argparse.ArgumentTypeError("region must be 4 finite numbers, not " + part)
        numbers.append(number)
    return numbers


def numbered(found: dict) -> dict[str, dict]:
    """The search results by the number printed for them: {"g1": ..., "e1": ..., "e2": ...}."""
    by_number = {}
    for source, letter, _ in SERVERS:
        for position, layer in enumerate(found["results"][source], start=1):
            by_number[letter + str(position)] = layer
    return by_number


def print_results(found: dict) -> None:
    """Print the search results per server, with the number to pick each by and its kind."""
    if found["nothing_close"]:
        print("Nothing in the layer list is close to " + repr(found["query"]) + ".")
        return

    for source, letter, heading in SERVERS:
        print(heading)
        layers = found["results"][source]
        if not layers:
            print("  (nothing)")
        for position, layer in enumerate(layers, start=1):
            number = letter + str(position)
            kind = layer.get("kind", "unknown")
            print("  {:<4} {:<9} {:<45} {:<20} {}".format(number, kind, layer["spoken"][:45], layer["group"][:20],
                                                         layer["name"]))
        print()
    print("Download one with: python layers.py download " + json.dumps(found["query"]) + " --pick e1")


def search(helper: str, query: str) -> dict:
    return post(helper, "/search", {"query": query, "per_server": 10})


def pick_layer(found: dict, pick: str) -> dict:
    """The search result a --pick names. A bare number is an EMODnet one: 3 means e3."""
    number = pick.strip().lower()
    if number.isdigit():
        number = "e" + number

    by_number = numbered(found)
    if number not in by_number:
        raise HelperError("there is no result " + pick + "; the search gave " + ", ".join(by_number))
    return by_number[number]


def print_summary(answer: dict) -> None:
    """Print what was downloaded: for shapes how many, for a picture its size."""
    print("Downloaded " + answer["title"] + " (" + answer["source"] + ", " + answer["name"] + ") as " + answer["kind"])
    print("  region     " + ",".join(format(value, ".0f") for value in answer["region"]))
    if "served_as" in answer:
        print("  served as  " + answer["served_as"] + " (the team GeoServer's DescribeLayer)")
    timings = answer["timings_ms"]
    describe = ""
    if "describe" in timings:
        describe = "describe {} ms, ".format(timings["describe"])
    if answer["kind"] == "picture":
        print("  picture    {} x {} pixels, {} bytes".format(answer["width"], answer["height"], answer["bytes"]))
        print("  took       {} ms ({}picture {} ms)".format(timings["total"], describe, timings["picture"]))
        return

    print("  shapes     " + str(answer["features"]) + " in the GeoJSON")
    if answer.get("held_back"):
        print("  held back  " + answer["held_back"])
    print("  took       {} ms ({}shapes {} ms)".format(timings["total"], describe, timings["shapes"]))


def write_out(path: str, answer: dict) -> None:
    """Write the downloaded file as it came: the PNG of a picture, the GeoJSON of shapes."""
    if answer["kind"] == "picture":
        Path(path).write_bytes(base64.b64decode(answer["png_base64"]))
        print("Wrote the picture to " + path)
        return
    with open(path, "w", encoding="utf-8") as file:
        json.dump(answer["geojson"], file)
    print("Wrote the GeoJSON to " + path)


def main() -> int:
    parser = argparse.ArgumentParser(description="Search the online layer list, and download one layer.")
    parser.add_argument("--helper", default=DEFAULT_HELPER, help="the voice interface, default " + DEFAULT_HELPER)
    commands = parser.add_subparsers(dest="command", required=True)

    search_command = commands.add_parser("search", help="list the closest layers on each server")
    search_command.add_argument("query", help='a few search words, such as "wind farms"')

    download_command = commands.add_parser("download", help="download one layer as the file its server sends")
    download_command.add_argument("query", nargs="?", help="search words, then --pick one of the results")
    download_command.add_argument("--pick", default="e1", help="which result: e3 (or 3), g2. Default e1")
    download_command.add_argument("--name", help="a catalogue name instead of a search, such as windfarmspoly")
    download_command.add_argument("--source", choices=("emodnet", "geoserver"), default="emodnet",
                                  help="where --name is: emodnet (default) or geoserver, the team GeoServer")
    download_command.add_argument("--region", type=parse_region, default=parse_region(DEFAULT_REGION),
                                  help="minx,miny,maxx,maxy in EPSG:3035, default the Belgian coast")
    download_command.add_argument("--out", help="write the downloaded file here: GeoJSON for shapes, PNG for a picture")
    download_command.add_argument("--as-picture", action="store_true",
                                  help="download the layer as the server draws it (a PNG), even when it has shapes")

    args = parser.parse_args()

    try:
        if args.command == "search":
            print_results(search(args.helper, args.query))
            return 0

        if args.name:
            source = args.source
            name = args.name
        elif args.query:
            found = search(args.helper, args.query)
            if found["nothing_close"]:
                print("Nothing in the layer list is close to " + repr(args.query) + ".")
                return 1
            layer = pick_layer(found, args.pick)
            print("Picked " + args.pick + ": " + layer["spoken"] + " (" + layer["source"] + ", " + layer["name"] + ")")
            source = layer["source"]
            name = layer["name"]
        else:
            parser.error("download needs search words or --name")

        body = {"source": source, "name": name, "region": args.region, "as_picture": args.as_picture}
        answer = post(args.helper, "/download", body)
    except HelperError as error:
        print("Refused: " + str(error), file=sys.stderr)
        return 1

    print_summary(answer)
    if args.out:
        write_out(args.out, answer)
    return 0


if __name__ == "__main__":
    sys.exit(main())

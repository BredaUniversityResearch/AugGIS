"""Command-line demo: every layer on both map servers, then find layers by voice.

    python layer_search.py

1. Lists every layer on the team's GeoServer and on EMODnet (about 1300).
2. Press Enter, speak ("load the wind farms"), press Enter.
3. Prints the matching layers from each server, with how many words each one shares.

Nothing is downloaded and nothing goes on a map. This shows what the matching finds.

    python layer_search.py --type          type requests instead of speaking
    python layer_search.py --cached        use the list saved last time, skip the servers
    python layer_search.py --shortlist     also print what the model chose from

Uses the same containers as voice.py, through the helper's POST /search.
"""

import argparse
import json
import shutil
import sys
from pathlib import Path

import requests

import voice
from layer_catalogue import load_catalogue
from voice import paint

HERE = Path(__file__).parent
CAPTURE = HERE / "_capture.wav"

SOURCE_TITLES = {"geoserver": "Team GeoServer", "emodnet": "EMODnet"}


def print_catalogue(layers: list[dict], counts: dict) -> None:
    """Print every layer, grouped by server and then by region or theme, in columns.

    Args:
        layers: The catalogue.
        counts: Layers per server, from load_catalogue().
    """
    width = shutil.get_terminal_size((120, 40)).columns - 4
    column = 44
    per_row = max(1, width // column)

    for source in ("geoserver", "emodnet"):
        in_source = []
        for layer in layers:
            if layer["source"] == source:
                in_source.append(layer)
        print()
        print(paint(f"{SOURCE_TITLES[source]}  {len(in_source)} layers", "cyan", "bold"))

        groups: dict[str, list[dict]] = {}
        for layer in in_source:
            groups.setdefault(layer["group"], []).append(layer)

        # Biggest group first, so the North Sea leads and GeoServer's demo data comes last.
        by_size = []
        for group, members in groups.items():
            by_size.append((len(members), group))
        by_size.sort(reverse=True)

        for size, group in by_size:
            members = groups[group]
            print(paint(f"  {group} ({len(members)})", "bold"))
            for start in range(0, len(members), per_row):
                cells = []
                for layer in members[start:start + per_row]:
                    cell = f"{layer['id']:<5} {layer['spoken']}"
                    if len(cell) > column - 2:
                        cell = cell[:column - 3] + "~"
                    cells.append(cell.ljust(column))
                print("    " + "".join(cells).rstrip())

    print()
    summary = []
    for server, count in counts.items():
        if count == 0:
            summary.append(paint(f"{server} did not answer", "yellow"))
        else:
            summary.append(f"{server} {count}")
    print(paint("  " + ", ".join(summary), "dim"))


def search(helper: str, layers: list[dict], wav: Path | None, text: str) -> dict:
    """Send a recording, or typed text, with the whole catalogue to the helper.

    Args:
        helper: Base URL of the helper service.
        layers: The catalogue. Only the four fields the helper needs are sent.
        wav: A recording, or None to send text.
        text: The request as text, used when wav is None.

    Returns:
        The helper's answer.

    Raises:
        RuntimeError: If the helper answers with anything but 200.
    """
    slim = []
    for layer in layers:
        slim.append({key: layer[key] for key in ("id", "source", "group", "spoken")})
    data = {"layers": json.dumps(slim), "text": text}

    if wav is None:
        response = requests.post(f"{helper.rstrip('/')}/search", data=data, timeout=300)
    else:
        with open(wav, "rb") as fh:
            response = requests.post(f"{helper.rstrip('/')}/search", data=data,
                                     files={"audio": (wav.name, fh, "audio/wav")}, timeout=300)

    if response.status_code != 200:
        raise RuntimeError(f"HTTP {response.status_code}: {response.text[:300]}")
    return response.json()


def show(result: dict, by_id: dict, show_shortlist: bool) -> None:
    """Print what was heard and the matches from each server.

    Args:
        result: The helper's answer.
        by_id: The catalogue by id.
        show_shortlist: Also print the layers the model chose from.
    """
    transcript = result.get("transcript") or ""
    if not transcript:
        print(paint("  heard  ", "dim") + paint("nothing", "yellow"))
        return
    print(paint("  heard  ", "dim") + paint(transcript, "bold"))

    if result.get("nothing_close"):
        print(paint("  no layer on either server is about that", "yellow"))

    for source in ("geoserver", "emodnet"):
        matches = result["matches"].get(source) or []
        print()
        print(paint(f"  {SOURCE_TITLES[source]}  ({len(matches)})", "cyan", "bold"))
        if not matches and not result.get("nothing_close"):
            print(paint("    no match", "yellow"))

        for match in matches:
            layer = by_id[match["id"]]
            print(f"    {match['words']:.2f}  {layer['id']:<5} {layer['spoken']}  |  {layer['group']}")

        if show_shortlist:
            picked = set()
            for match in matches:
                picked.add(match["id"])
            print(paint("    chose from:", "dim"))
            for layer_id in result["shortlists"].get(source) or []:
                marker = "+" if layer_id in picked else " "
                print(paint(f"      {marker} {layer_id:<5} {by_id[layer_id]['spoken']}", "dim"))

    timings = result.get("timings_ms") or {}
    print()
    print(paint(f"  {timings.get('stt', '?')}ms speech + {timings.get('search', '?')}ms search "
                f"= {timings.get('total', '?')}ms", "dim"))


def parse_args() -> argparse.Namespace:
    """Read the command line."""
    parser = argparse.ArgumentParser(
        description="List every layer on both map servers, then find layers by voice.")
    parser.add_argument("--type", action="store_true", help="type requests instead of speaking")
    parser.add_argument("--text", help="search for this one request and exit")
    parser.add_argument("--wav", type=Path, help="send this WAV instead of recording, then exit")
    parser.add_argument("--cached", action="store_true",
                        help="use the list saved last time instead of asking the servers")
    parser.add_argument("--shortlist", action="store_true",
                        help="also print the shortlist the model chose from")
    parser.add_argument("--no-list", action="store_true", help="do not print every layer")
    parser.add_argument("--mic", help="microphone name, see python voice.py --list-mics")
    parser.add_argument("--seconds", type=int,
                        help="record a fixed number of seconds instead of until Enter")
    parser.add_argument("--no-start", action="store_true",
                        help="do not start the containers if they are down")
    parser.add_argument("--helper", default="http://localhost:8080")
    return parser.parse_args()


def main() -> None:
    """Run the demo."""
    args = parse_args()

    print(paint("\nAugGIS layer search", "cyan", "bold"))
    if args.cached:
        print(paint("  reading the saved layer list", "dim"))
    else:
        print(paint("  asking the team's GeoServer and 7 EMODnet servers for their layers", "dim"))
    layers, counts = load_catalogue(refresh=not args.cached)

    by_id = {}
    for layer in layers:
        by_id[layer["id"]] = layer

    if not args.no_list:
        print_catalogue(layers, counts)
    print(paint(f"  {len(layers)} layers in total", "bold"))

    voice.ensure_ready(args.helper, start=not args.no_start)

    if args.text:
        print()
        show(search(args.helper, layers, None, args.text), by_id, args.shortlist)
        return
    if args.wav:
        print()
        show(search(args.helper, layers, args.wav, ""), by_id, args.shortlist)
        return

    mic = None
    if not args.type:
        mic = voice.pick_mic(args.mic)

    while True:
        print()
        try:
            if args.type:
                request = input(paint("  type a request, Ctrl+C to quit: ", "dim")).strip()
                if not request:
                    continue
                show(search(args.helper, layers, None, request), by_id, args.shortlist)
                continue

            input(paint("  press Enter and speak, Ctrl+C to quit ", "dim"))
            voice.record(mic, CAPTURE, args.seconds, args.seconds is None)

            peak = voice.peak_amplitude(CAPTURE)
            if peak < voice.SILENT_PEAK:
                print(paint(f"  that recording is silent (peak {peak:.4f}). "
                            f"{mic} is not picking anything up.", "yellow"))
                continue

            print(paint("  working...", "dim"))
            show(search(args.helper, layers, CAPTURE, ""), by_id, args.shortlist)
        except (EOFError, KeyboardInterrupt):
            print("\n  bye")
            return
        except Exception as exc:
            print(paint(f"  {exc}", "red"))


if __name__ == "__main__":
    sys.exit(main())

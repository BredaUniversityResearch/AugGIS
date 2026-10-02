"""Command-line demo: every layer on both map servers, then find layers by voice.

    python layer_search.py

1. Lists every layer on the team's GeoServer and on EMODnet (about 1300).
2. Press Enter, speak ("load the wind farms"), press Enter.
3. Prints the matching layers from each server, with how close each one is.

Nothing is downloaded and nothing goes on a map. This shows what the matching finds.

    python layer_search.py --type          type requests instead of speaking
    python layer_search.py --cached        use the list saved last time, skip the servers
    python layer_search.py --shortlist     also print what the model chose from
    python layer_search.py --no-model      pick by scores alone, to compare with the model
    python layer_search.py --chat          talk it down: "no, search for bathymetry instead"

Uses the same containers as voice.py, through the helper's POST /search, or POST /chat with
--chat. In --chat the conversation is kept between sentences, so a follow-up continues from
what was found; type "new" to start over.
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

# A pick whose meaning score is this far below the best pick on either server is shown dimmed.
# The model tends to fill all 6 places, and this is how the padding shows itself.
WEAK_GAP = 0.12


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


def search(helper: str, layers: list[dict], wav: Path | None, text: str, use_model: bool = True) -> dict:
    """Send a recording, or typed text, with the whole catalogue to the helper.

    Args:
        helper: Base URL of the helper service.
        layers: The catalogue. Only the four fields the helper needs are sent.
        wav: A recording, or None to send text.
        text: The request as text, used when wav is None.
        use_model: False asks the helper to pick by scores alone.

    Returns:
        The helper's answer.

    Raises:
        RuntimeError: If the helper answers with anything but 200.
    """
    slim = []
    for layer in layers:
        slim.append({key: layer[key] for key in ("id", "source", "group", "spoken")})
    model_field = "true" if use_model else "false"
    data = {"layers": json.dumps(slim), "text": text, "model": model_field}

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

    # The best pick on either server is the yardstick for "weak".
    best = 0.0
    for source in ("geoserver", "emodnet"):
        for match in result["matches"].get(source) or []:
            best = max(best, match["meaning"])

    for source in ("geoserver", "emodnet"):
        matches = result["matches"].get(source) or []
        print()
        print(paint(f"  {SOURCE_TITLES[source]}  ({len(matches)})", "cyan", "bold"))
        if not matches and not result.get("nothing_close"):
            print(paint("    no match", "yellow"))

        for match in matches:
            layer = by_id[match["id"]]
            line = f"    {match['meaning']:.2f}  {layer['id']:<5} {layer['spoken']}  |  {layer['group']}"
            if match["meaning"] < best - WEAK_GAP:
                print(paint(line + "  (weak)", "dim"))
            else:
                print(line)

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


def send_chat(helper: str, layers: list[dict], wav: Path | None, text: str, conversation: dict) -> dict:
    """Send one sentence of the conversation, spoken or typed, to the helper.

    Args:
        helper: Base URL of the helper service.
        layers: The catalogue. Only the four fields the helper needs are sent.
        wav: A recording, or None to send text.
        text: The sentence as text, used when wav is None.
        conversation: The "conversation" from the previous reply, or {} to start.

    Returns:
        The helper's answer, whose "conversation" goes back in with the next sentence.

    Raises:
        RuntimeError: If the helper answers with anything but 200.
    """
    slim = []
    for layer in layers:
        slim.append({key: layer[key] for key in ("id", "source", "group", "spoken")})
    data = {"layers": json.dumps(slim), "text": text, "conversation": json.dumps(conversation)}

    if wav is None:
        response = requests.post(f"{helper.rstrip('/')}/chat", data=data, timeout=300)
    else:
        with open(wav, "rb") as fh:
            response = requests.post(f"{helper.rstrip('/')}/chat", data=data,
                                     files={"audio": (wav.name, fh, "audio/wav")}, timeout=300)

    if response.status_code != 200:
        raise RuntimeError(f"HTTP {response.status_code}: {response.text[:300]}")
    return response.json()


def show_chat(result: dict, by_id: dict) -> None:
    """Print one turn: what was heard, each search's raw top 10 with scores, then the reply and picks.

    Args:
        result: The helper's answer to /chat.
        by_id: The catalogue by id.
    """
    transcript = result.get("transcript") or ""
    if not transcript:
        print(paint("  heard  ", "dim") + paint("nothing", "yellow"))
        return
    print(paint("  heard  ", "dim") + paint(transcript, "bold"))

    searches = result.get("searches") or []
    if not searches:
        print(paint("  no new search, answered from what was already found", "dim"))
    for done in searches:
        print()
        if done["nothing_close"]:
            print(paint(f"  searched \"{done['query']}\": nothing in the catalogue is close", "yellow"))
            continue
        print(paint(f"  searched \"{done['query']}\"   meaning  words", "cyan"))
        for source in ("geoserver", "emodnet"):
            print(paint(f"    {SOURCE_TITLES[source]}, top {len(done['found'][source])}", "dim"))
            for layer_id in done["found"][source]:
                layer = by_id[layer_id]
                score = done["scores"][layer_id]
                print(paint(f"      {score['meaning']:.2f}   {score['words']:.2f}   {layer['id']:<5} "
                            f"{layer['spoken']}  |  {layer['group']}", "dim"))

    for problem in result.get("problems") or []:
        print(paint(f"  the model's reply was not a step: {problem[:120]}", "yellow"))

    print()
    reply = result.get("reply") or ""
    if reply:
        print(paint("  model  ", "dim") + paint(reply, "bold"))

    for source in ("geoserver", "emodnet"):
        picks = result["picks"].get(source) or []
        print()
        print(paint(f"  {SOURCE_TITLES[source]}, picked  ({len(picks)})", "cyan", "bold"))
        for layer_id in picks:
            layer = by_id[layer_id]
            print(f"    {layer['id']:<5} {layer['spoken']}  |  {layer['group']}")

    timings = result.get("timings_ms") or {}
    print()
    print(paint(f"  {timings.get('stt', '?')}ms speech + {timings.get('chat', '?')}ms "
                f"{result.get('backend', 'model')} = {timings.get('total', '?')}ms", "dim"))


def run_chat(args: argparse.Namespace, layers: list[dict], by_id: dict) -> None:
    """The conversation loop: each sentence continues from the last until "new" is typed.

    Args:
        args: The command line.
        layers: The catalogue.
        by_id: The catalogue by id.
    """
    conversation = {}

    # --say runs a scripted conversation, one sentence per --say, then exits.
    if args.say:
        for sentence in args.say:
            print()
            result = send_chat(args.helper, layers, None, sentence, conversation)
            conversation = result["conversation"]
            show_chat(result, by_id)
        return

    mic = None
    if not args.type:
        mic = voice.pick_mic(args.mic)
    print(paint('  type "new" at the prompt to start a new conversation', "dim"))

    while True:
        print()
        try:
            if args.type:
                typed = input(paint("  you: ", "dim")).strip()
            else:
                typed = input(paint("  press Enter and speak, Ctrl+C to quit ", "dim")).strip()

            if typed.lower() == "new":
                conversation = {}
                print(paint("  new conversation", "green"))
                continue

            if args.type:
                if not typed:
                    continue
                result = send_chat(args.helper, layers, None, typed, conversation)
            else:
                voice.record(mic, CAPTURE, args.seconds, args.seconds is None)
                peak = voice.peak_amplitude(CAPTURE)
                if peak < voice.SILENT_PEAK:
                    print(paint(f"  that recording is silent (peak {peak:.4f}). "
                                f"{mic} is not picking anything up.", "yellow"))
                    continue
                print(paint("  working...", "dim"))
                result = send_chat(args.helper, layers, CAPTURE, "", conversation)

            conversation = result["conversation"]
            show_chat(result, by_id)
        except (EOFError, KeyboardInterrupt):
            print("\n  bye")
            return
        except Exception as exc:
            print(paint(f"  {exc}", "red"))


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
    parser.add_argument("--chat", action="store_true",
                        help="keep the conversation, so a follow-up can change the search")
    parser.add_argument("--say", action="append",
                        help="with --chat: one sentence of a scripted conversation, repeatable")
    parser.add_argument("--no-model", action="store_true",
                        help="pick by scores alone, without the language model")
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

    # The first search works out a meaning vector for every layer, about 11 seconds for 1300.
    # Doing it now keeps that wait off the first spoken request.
    print(paint("  preparing the layers for matching...", "dim"))
    search(args.helper, layers, None, "wind farms")

    if args.chat:
        run_chat(args, layers, by_id)
        return

    if args.text:
        print()
        show(search(args.helper, layers, None, args.text, not args.no_model), by_id, args.shortlist)
        return
    if args.wav:
        print()
        show(search(args.helper, layers, args.wav, "", not args.no_model), by_id, args.shortlist)
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
                show(search(args.helper, layers, None, request, not args.no_model), by_id, args.shortlist)
                continue

            input(paint("  press Enter and speak, Ctrl+C to quit ", "dim"))
            voice.record(mic, CAPTURE, args.seconds, args.seconds is None)

            peak = voice.peak_amplitude(CAPTURE)
            if peak < voice.SILENT_PEAK:
                print(paint(f"  that recording is silent (peak {peak:.4f}). "
                            f"{mic} is not picking anything up.", "yellow"))
                continue

            print(paint("  working...", "dim"))
            show(search(args.helper, layers, CAPTURE, "", not args.no_model), by_id, args.shortlist)
        except (EOFError, KeyboardInterrupt):
            print("\n  bye")
            return
        except Exception as exc:
            print(paint(f"  {exc}", "red"))


if __name__ == "__main__":
    sys.exit(main())

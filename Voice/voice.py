"""Command-line client for the AugGIS voice helper.

Give it layer names, speak, get the layer operations back.

    python voice.py --layers "Population,Bathymetry,Shipping lanes"

Starts the containers if they are not running, then loops: press Enter, speak, read the
result. Ctrl+C to quit.

The layer names are an argument because that is how the helper works. It builds the JSON
schema's enum from whatever list the caller sends, so the model cannot name a layer that
is not in the session. Unity will send LayerManager.AllLayers. This sends what was typed.

Needs ffmpeg on PATH for the microphone and `requests` for the HTTP calls. No audio
library, nothing to pip install beyond requests.
"""

import argparse
import array
import json
import re
import subprocess
import sys
import time
import wave
from pathlib import Path

import requests

HERE = Path(__file__).parent
STACK = HERE / "voice-stack.yml"
CAPTURE = HERE / "_capture.wav"

# 16 kHz mono 16-bit PCM, matching what the headset sends. 
SAMPLE_RATE = "16000"
VIRTUAL_DEVICES = ("virtual", "steam streaming", "voicemeeter", "cable", "vb-audio")
SILENT_PEAK = 0.01

STYLE = {"dim": "\033[2m", "bold": "\033[1m", "green": "\033[32m",
         "yellow": "\033[33m", "red": "\033[31m", "cyan": "\033[36m", "off": "\033[0m"}


def paint(text: str, *styles: str) -> str:
    """Wrap text in terminal colour codes.

    Args:
        text: The text to colour.
        *styles: Names from STYLE.

    Returns:
        The text with escape codes around it.
    """
    return "".join(STYLE[s] for s in styles) + text + STYLE["off"]


def list_mics() -> list[str]:
    """Ask ffmpeg which audio inputs exist.

    Returns:
        The device names, in the form ffmpeg wants them back as. Empty if none.
    """
    result = subprocess.run(
        ["ffmpeg", "-hide_banner", "-list_devices", "true", "-f", "dshow", "-i", "dummy"],
        capture_output=True, text=True,
    )
    # ffmpeg prints the device list to stderr, not stdout.
    return re.findall(r'"([^"]+)"\s+\(audio\)', result.stderr)


def record(mic: str, target: Path, seconds: int, push_to_talk: bool) -> None:
    """Record from the microphone into a WAV file.

    Args:
        mic: Device name from list_mics().
        target: Where to write the WAV.
        seconds: Fixed length, ignored when push_to_talk is set.
        push_to_talk: Record until Enter is pressed instead of a fixed length.
    """
    command = [
        "ffmpeg", "-hide_banner", "-loglevel", "error", "-y",
        "-f", "dshow", "-i", f"audio={mic}",
        "-ar", SAMPLE_RATE, "-ac", "1", "-acodec", "pcm_s16le",
    ]

    if push_to_talk:
        # Send "q" rather than killing the process. A WAV header records the length at
        # the front of the file, and ffmpeg only writes the real number when it shuts
        # down cleanly. A killed process leaves a file that reads as zero length.
        process = subprocess.Popen(command + [str(target)], stdin=subprocess.PIPE)
        input(paint("  recording, press Enter to stop ", "red", "bold"))
        process.communicate(input=b"q", timeout=10)
    else:
        print(paint(f"  recording {seconds}s, speak now", "red", "bold"))
        subprocess.run(command + ["-t", str(seconds), str(target)], check=True)


def peak_amplitude(wav: Path) -> float:
    """Measure the loudest moment in a recording.

    Args:
        wav: A 16-bit PCM WAV file.

    Returns:
        The largest sample as a fraction of full scale, 0.0 to 1.0. Returns 0.0 if the
        file cannot be read as 16-bit PCM.
    """
    try:
        with wave.open(str(wav), "rb") as handle:
            if handle.getsampwidth() != 2:
                return 0.0
            samples = array.array("h", handle.readframes(handle.getnframes()))
    except Exception:
        return 0.0

    return max((abs(s) for s in samples), default=0) / 32768.0


def send(helper: str, wav: Path, layers: list[str], trim: bool) -> dict:
    """Post a recording and its layer names to the helper.

    Args:
        helper: Base URL of the helper service.
        wav: The recording to send.
        layers: Layer names for this session.
        trim: Whether to run Whisper's silence filter.

    Returns:
        The parsed response.

    Raises:
        RuntimeError: If the helper answers with anything but 200. 400 means this client
            sent something wrong, 503 means a model service did not answer.
    """
    with open(wav, "rb") as fh:
        response = requests.post(
            f"{helper.rstrip('/')}/command",
            files={"audio": (wav.name, fh, "audio/wav")},
            data={"layers": json.dumps(layers), "trim": "true" if trim else "false"},
            timeout=180,
        )

    if response.status_code != 200:
        raise RuntimeError(f"HTTP {response.status_code}: {response.text[:300]}")
    return response.json()


def show(result: dict) -> None:
    """Print a result the way the demo should read.

    Args:
        result: A response from the helper.
    """
    transcript = result.get("transcript") or ""
    timings = result.get("timings_ms") or {}

    if transcript:
        print(paint("  heard  ", "dim") + paint(transcript, "bold"))
    else:
        print(paint("  heard  ", "dim") + paint("nothing", "yellow"))

    for operation in result.get("operations") or []:
        if operation.get("action") == "none":
            print(paint("  ->     ", "dim") + paint("no layer command in that", "yellow"))
        else:
            print(paint("  ->     ", "dim")
                  + paint(operation["action"], "green", "bold")
                  + "  " + ", ".join(operation["layers"]))

    print(paint(f"  {timings.get('stt', '?')}ms speech + {timings.get('llm', '?')}ms intent "
                f"= {timings.get('total', '?')}ms", "dim"))


def stack_is_ready(helper: str) -> tuple[bool, str]:
    """Ask the helper whether both model services are reachable.

    Args:
        helper: Base URL of the helper service.

    Returns:
        Whether it is ready, and a line describing the state.
    """
    try:
        health = requests.get(f"{helper.rstrip('/')}/health", timeout=10).json()
    except Exception as exc:
        return False, f"helper not answering ({type(exc).__name__})"

    if health.get("ready"):
        return True, "whisper ok, llm ok"

    missing = [f"{k}={v}" for k, v in health.items() if k in ("whisper", "llm") and v != "ok"]
    return False, ", ".join(missing) or "not ready"


def start_stack() -> None:
    """Bring the containers up and wait for the models to answer.

    Whisper loads its model on first start, so the wait is real rather than defensive.
    """
    print(paint("  starting containers", "dim"))
    subprocess.run(["docker", "compose", "-f", str(STACK), "up", "-d"], check=True)


def ensure_ready(helper: str, start: bool) -> None:
    """Make sure the stack is up, starting it if asked.

    Args:
        helper: Base URL of the helper service.
        start: Whether to run docker compose when nothing answers.

    Raises:
        SystemExit: If the stack is not ready and cannot be started.
    """
    ready, detail = stack_is_ready(helper)
    if ready:
        print(paint(f"  helper ready ({detail})", "green"))
        return

    if not start:
        raise SystemExit(paint(f"  helper not ready: {detail}\n"
                               f"  start it: docker compose -f voice-stack.yml up -d", "red"))

    try:
        start_stack()
    except FileNotFoundError:
        raise SystemExit(paint("  docker not found. Start Docker Desktop first.", "red"))
    except subprocess.CalledProcessError as exc:
        raise SystemExit(paint(f"  docker compose failed ({exc.returncode}). "
                               f"Is Docker Desktop running?", "red"))

    # Whisper downloads and loads its model on a cold start, which is slow the first time.
    for _ in range(60):
        ready, detail = stack_is_ready(helper)
        if ready:
            print(paint(f"  helper ready ({detail})", "green"))
            return
        time.sleep(5)

    raise SystemExit(paint(f"  containers started but never became ready: {detail}", "red"))


def pick_mic(requested: str | None) -> str:
    """Choose which microphone to record from.

    Args:
        requested: A name given on the command line, or None to pick the first found.

    Returns:
        The device name to pass to ffmpeg.

    Raises:
        SystemExit: If no microphone was found, or the requested one does not exist.
    """
    available = list_mics()
    if not available:
        raise SystemExit(paint("  no microphones found. Use --wav to send a file instead.", "red"))

    if requested:
        if requested not in available:
            names = "\n".join(f"    {m}" for m in available)
            raise SystemExit(paint(f"  no microphone called {requested!r}. Found:\n{names}", "red"))
        return requested

    # Virtual devices appear in the list and record silence, so picking the first one
    # found gives "heard nothing" on every command with nothing to say why.
    real = [m for m in available if not any(word in m.lower() for word in VIRTUAL_DEVICES)]
    chosen = real[0] if real else available[0]

    print(paint(f"  mic    {chosen}  (--mic to change, --list-mics to see all)", "dim"))
    return chosen


def parse_args() -> argparse.Namespace:
    """Read the command line.

    Returns:
        The parsed arguments.
    """
    parser = argparse.ArgumentParser(
        description="Speak at the laptop, get AugGIS layer commands back.")
    parser.add_argument("--layers",
                        help='comma-separated layer names, e.g. "Population,Bathymetry"')
    parser.add_argument("--mic", help="microphone name, see --list-mics")
    parser.add_argument("--list-mics", action="store_true", help="list microphones and exit")
    parser.add_argument("--wav", type=Path, help="send this WAV instead of recording")
    parser.add_argument("--seconds", type=int,
                        help="record a fixed number of seconds instead of until Enter")
    parser.add_argument("--once", action="store_true", help="one command then exit")
    parser.add_argument("--no-trim", action="store_true",
                        help="turn Whisper's silence filter off")
    parser.add_argument("--no-start", action="store_true",
                        help="do not start the containers if they are down")
    parser.add_argument("--helper", default="http://localhost:8080")
    return parser.parse_args()


def main() -> None:
    """Run the demo."""
    args = parse_args()

    if args.list_mics:
        for mic in list_mics():
            print(f'  --mic "{mic}"')
        return

    if not args.layers:
        raise SystemExit('give me --layers "Population,Bathymetry,Shipping lanes"')
    layers = [name.strip() for name in args.layers.split(",") if name.strip()]

    print(paint("\nAugGIS voice", "cyan", "bold"))
    print(paint("  layers ", "dim") + ", ".join(layers))
    ensure_ready(args.helper, start=not args.no_start)

    if args.wav:
        print()
        show(send(args.helper, args.wav, layers, not args.no_trim))
        return

    mic = pick_mic(args.mic)

    while True:
        print()
        try:
            input(paint("  press Enter to start, Ctrl+C to quit ", "dim"))
            record(mic, CAPTURE, args.seconds, args.seconds is None)

            # A silent recording is almost always the wrong microphone, not a quiet
            # speaker. Say that here rather than letting it look like a transcription
            # that heard nothing.
            peak = peak_amplitude(CAPTURE)
            if peak < SILENT_PEAK:
                print(paint(f"  that recording is silent (peak {peak:.4f}). "
                            f"{mic} is not picking anything up.", "yellow"))
                print(paint("  run --list-mics and pass a different --mic", "yellow"))
                continue

            # Transcribing and resolving takes about a second warm and 20 seconds cold,
            # with nothing to look at in between.
            print(paint("  working...", "dim"))
            show(send(args.helper, CAPTURE, layers, not args.no_trim))
        except (EOFError, KeyboardInterrupt):
            print("\n  bye")
            return
        except Exception as exc:
            print(paint(f"  {exc}", "red"))

        if args.once:
            return


if __name__ == "__main__":
    sys.exit(main())

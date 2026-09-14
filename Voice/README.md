# VoiceCLI

Give it layer names, speak, get layer commands back. No headset, no Unity, no AugGIS server.

Commands to copy-paste are in `commands.txt`.

## Run

```powershell
docker compose -f voice-stack.yml up -d
python voice.py --layers "Population,Bathymetry,Shipping lanes,Wind farms"
```

Press Enter, speak, Ctrl+C to quit. Or double-click `run-demo.cmd`.

Say "turn off bathymetry and show the shipping lanes" and it prints two operations.

## Shape

```
voice.py  ->  helper :8080  ->  whisper :9000  ->  llm :11434
mic, WAV      coordinates      transcript         operations
```

1) Records the mic with ffmpeg, 16 kHz mono
2) Posts the WAV and the layer names to the helper
3) Helper gets a transcript from Whisper
4) Helper sends the transcript to the LLM with the layer names as a JSON schema enum
5) Prints the transcript, the operations and the timings

## Options

| Flag | What |
| --- | --- |
| `--layers "A,B,C"` | Layer names. Required. |
| `--list-mics` | Lists microphones |
| `--mic "name"` | Which microphone. Defaults to the first real one. |
| `--seconds N` | Record a fixed N seconds instead of until Enter |
| `--wav file.wav` | Send a file instead of recording |
| `--once` | One command then exit |
| `--no-trim` | Whisper silence filter off |
| `--no-start` | Do not start the containers |
| `--helper URL` | Default `http://localhost:8080` |

## Layer names are an argument

The helper builds the JSON schema enum from the list the caller sends, and the model is
constrained to that enum, so it cannot name a layer that is not in the session. Session
layers come from whatever config was exported, so they are not known when the helper
starts.

Unity sends `LayerManager.AllLayers` to the same endpoint. This sends what was typed.

## Files

| File | What |
| --- | --- |
| `voice.py` | The client |
| `helper/app.py` | The service, `POST /command` and `GET /health` |
| `helper/prompt.json` | System prompt and examples. Bind-mounted, restart applies it in 3s. |
| `voice-stack.yml` | 3 containers, only the helper publishes a port |
| `commands.txt` | Every command, to copy-paste |

## Caught me out

1) Ollama unloads an idle model after 5 minutes, putting a 20 second wait on whoever speaks
next. Set `keep_alive` on the request and `OLLAMA_KEEP_ALIVE` on the container.

2) Whisper cannot report hearing nothing, it always produces plausible text. Given silence
and the layer names as expected vocabulary it returns the layer names, and the LLM turns
them on. The helper drops any transcript made only of layer-name words.

3) The GPU image is 31.8 GB, the CUDA runtime rather than the model.

4) Check the GPU from inside the container. A silent fall back to the processor looks like
nothing until you time it.

5) Stop ffmpeg by sending it `q`. A killed recording reads as zero length.

## Needs

ffmpeg on PATH, `requests`, Docker Desktop, NVIDIA GPU.

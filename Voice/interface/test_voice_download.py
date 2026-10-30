"""Tests for a download said to the headset: the result that goes back, the download step in a
turn, and the region field of POST /voice.

    python -m pytest Voice/interface

No server is asked. A fake (httpx.MockTransport) answers for the language model and the search
service, so these run without the containers.
"""

import asyncio
import importlib.util
import json
from pathlib import Path

import httpx
from fastapi.testclient import TestClient

import conversation

HERE = Path(__file__).parent
REAL_CLIENT = httpx.AsyncClient
REGION = [3797132.0, 3137432.0, 3965925.0, 3291676.0]
RESULT_FIELDS = {"ok", "source", "name", "title", "kind", "features", "line", "took_ms"}

WIND_FARMS = {"id": "E4", "source": "emodnet", "group": "Human Activities", "name": "windfarmspoly",
              "spoken": "Wind Farms (Polygons)", "kind": "shapes"}
CABLES = {"id": "G7", "source": "geoserver", "group": "North Sea", "name": "northsee:NS_Telecom_Cables_Emodnet_2026",
          "spoken": "NS Telecom Cables Emodnet 2026", "kind": "unknown"}

SHAPES_ANSWER = {
    "kind": "shapes", "source": "emodnet", "name": "windfarmspoly", "region": REGION,
    "title": "Wind Farms (Polygons)", "features": 48, "matched": 48, "held_back": "",
    "geojson": {"type": "FeatureCollection",
                "features": [{"type": "Feature", "geometry": {"type": "Point", "coordinates": [1, 2]}}]},
    "timings_ms": {"shapes": 900, "total": 910},
}
PICTURE_ANSWER = {
    "kind": "picture", "source": "geoserver", "name": "northsee:NS_Bathymetry_Raster", "region": REGION,
    "title": "Bathymetry Raster", "width": 1024, "height": 939,
    "bytes": 50000, "png_base64": "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==",
    "timings_ms": {"picture": 700, "total": 800},
}


def assert_nothing_of_the_layer(result: dict) -> None:
    """A download result holds counts and one line, never the layer itself."""
    assert set(result) == RESULT_FIELDS
    text = json.dumps(result)
    for field in ("geojson", "png_base64", "coordinates", "geometry"):
        assert '"' + field + '"' not in text
    assert "iVBORw0KGgo" not in text


def test_shapes_give_their_counts_in_one_line():
    result = conversation.download_result(WIND_FARMS, 200, SHAPES_ANSWER, 910)
    assert result == {
        "ok": True, "source": "emodnet", "name": "windfarmspoly", "title": "Wind Farms (Polygons)",
        "kind": "shapes", "features": 48,
        "line": "Downloaded Wind Farms (Polygons) (Human Activities, EMODnet) on the helper: 48 shapes.",
        "took_ms": 910,
    }
    assert_nothing_of_the_layer(result)


def test_a_picture_gives_its_size_and_not_the_png():
    layer = {"source": "geoserver", "name": "northsee:NS_Bathymetry_Raster", "spoken": "NS Bathymetry Raster",
             "group": "North Sea"}
    result = conversation.download_result(layer, 200, PICTURE_ANSWER, 800)
    assert result["ok"] is True
    assert result["kind"] == "picture"
    assert result["line"] == "Downloaded Bathymetry Raster (North Sea, team GeoServer) on the helper: picture 1024×939."
    assert result["features"] == 0
    assert_nothing_of_the_layer(result)


def test_a_refusal_is_a_result_with_the_refusal_sentence():
    sentence = "Nothing from Wind Farms (Polygons) lies in this region."
    result = conversation.download_result(WIND_FARMS, 422, {"error": sentence}, 300)
    assert result == {
        "ok": False, "source": "emodnet", "name": "windfarmspoly", "title": "Wind Farms (Polygons)",
        "kind": "", "features": 0, "line": "Not downloaded. " + sentence + " (Human Activities, EMODnet)",
        "took_ms": 300,
    }


def test_a_refusal_without_a_sentence_still_gets_one():
    result = conversation.download_result(WIND_FARMS, 500, {}, 10)
    assert result["ok"] is False
    assert result["line"] == "Not downloaded. The download of Wind Farms (Polygons) failed (HTTP 500). (Human Activities, EMODnet)"


class Fake:
    """Answers for the BUas language model and the search service, from a script of model replies."""

    def __init__(self, model_replies: list[dict], download_status: int = 200, download_answer: dict | None = None,
                 version: str = "v1", download_fails: bool = False, lookup_status: int = 200):
        self.model_replies = list(model_replies)
        self.download_status = download_status
        self.download_answer = download_answer
        if download_answer is None:
            self.download_answer = SHAPES_ANSWER
        self.version = version
        self.download_fails = download_fails
        self.lookup_status = lookup_status
        self.messages: list[str] = []
        self.downloads: list[dict] = []
        self.searches: list[str] = []

    def answer(self, request: httpx.Request) -> httpx.Response:
        path = request.url.path
        if path.endswith("/init_assistant"):
            return httpx.Response(200, json={"chat_key": "k-new"})
        body = json.loads(request.content)
        if path.endswith("/ask_assistant"):
            self.messages.append(body["message"])
            return httpx.Response(200, text=json.dumps(self.model_replies.pop(0)))
        if path == "/search":
            self.searches.append(body["query"])
            results = {"geoserver": [dict(CABLES, meaning=0.7)], "emodnet": [dict(WIND_FARMS, meaning=0.9)]}
            return httpx.Response(200, json={"query": body["query"], "version": self.version,
                                             "nothing_close": False, "results": results})
        if path == "/layers":
            if self.lookup_status != 200:
                return httpx.Response(self.lookup_status, json={"error": "the layer list is not ready yet"})
            found = []
            for layer in (WIND_FARMS, CABLES):
                if layer["id"] in body["ids"]:
                    found.append(layer)
            return httpx.Response(200, json={"version": self.version, "layers": found})
        if path == "/download":
            self.downloads.append(body)
            if self.download_fails:
                raise httpx.ReadTimeout("too slow", request=request)
            return httpx.Response(self.download_status, json=self.download_answer)
        return httpx.Response(404, json={"error": "no such path " + path})


def run_turn(monkeypatch, fake: Fake, sentence: str, ticket: dict, region=None) -> dict:
    def fake_client(**kwargs):
        return REAL_CLIENT(transport=httpx.MockTransport(fake.answer), **kwargs)

    monkeypatch.setattr(conversation.httpx, "AsyncClient", fake_client)
    monkeypatch.setattr(conversation, "VOICE_PROMPT_PATH", str(HERE / "voice-prompt.json"))
    return asyncio.run(conversation.turn(sentence, ["Bathymetry"], ticket, region))


FOUND_TICKET = {"chat_key": "k1", "found": [{"id": "E4", "meaning": 0.9}, {"id": "G7", "meaning": 0.7}],
                "version": "v1"}


def test_download_of_a_layer_found_earlier_gives_its_line_as_the_reply(monkeypatch):
    fake = Fake([{"action": "download", "id": "E4", "reply": "Downloading it."}])
    region = [3800000.0, 3140000.0, 3900000.0, 3200000.0]
    result = run_turn(monkeypatch, fake, "download the first one", FOUND_TICKET, region)

    assert fake.downloads == [{"source": "emodnet", "name": "windfarmspoly", "region": region}]
    assert len(result["downloads"]) == 1
    assert result["downloads"][0]["line"] == ("Downloaded Wind Farms (Polygons) (Human Activities, EMODnet) on the helper: "
                                             + "48 shapes.")
    assert result["reply"] == result["downloads"][0]["line"]
    assert_nothing_of_the_layer(result["downloads"][0])
    assert result["operations"] == []
    assert result["picks"] == {"geoserver": [], "emodnet": []}
    assert result["conversation"]["found"] == FOUND_TICKET["found"]


def test_a_download_without_a_region_uses_the_belgian_box(monkeypatch):
    fake = Fake([{"action": "download", "id": "e4", "reply": ""}])
    run_turn(monkeypatch, fake, "download the wind farms", FOUND_TICKET)
    assert fake.downloads[0]["region"] == REGION


def test_a_one_shot_download_searches_first_then_downloads_what_it_found(monkeypatch):
    fake = Fake([
        {"action": "search", "query": "wind farms"},
        {"action": "download", "id": "E4", "reply": "Downloading the wind farms."},
    ])
    result = run_turn(monkeypatch, fake, "download the wind farms", {})

    assert fake.searches == ["wind farms"]
    assert fake.downloads[0]["name"] == "windfarmspoly"
    assert result["downloads"][0]["ok"] is True
    assert result["searches"] == [{"query": "wind farms", "nothing_close": False}]


def test_a_download_of_an_id_nobody_found_is_sent_back_to_the_model(monkeypatch):
    fake = Fake([
        {"action": "download", "id": "E999", "reply": "Downloading."},
        {"action": "search", "query": "wind farms"},
        {"action": "download", "id": "E4", "reply": "Downloading."},
    ])
    result = run_turn(monkeypatch, fake, "download the wind farms", {})

    assert "E999" in fake.messages[1]
    assert "No search in this conversation found" in fake.messages[1]
    assert result["problems"] == ["the model asked to download E999, which no search in this conversation found"]
    assert len(fake.downloads) == 1
    assert result["downloads"][0]["name"] == "windfarmspoly"


def test_a_refused_download_is_a_result_and_not_an_error(monkeypatch):
    sentence = "Nothing from Wind Farms (Polygons) lies in this region."
    fake = Fake([{"action": "download", "id": "E4", "reply": "Downloading."}], download_status=422,
                download_answer={"error": sentence})
    result = run_turn(monkeypatch, fake, "download the first one", FOUND_TICKET)

    assert result["downloads"][0]["ok"] is False
    assert result["downloads"][0]["kind"] == ""
    assert result["downloads"][0]["line"] == "Not downloaded. " + sentence + " (Human Activities, EMODnet)"
    assert result["reply"] == result["downloads"][0]["line"]


def test_a_download_the_search_service_never_finishes_is_a_result(monkeypatch):
    fake = Fake([{"action": "download", "id": "G7", "reply": "Downloading."}], download_fails=True)
    result = run_turn(monkeypatch, fake, "download the cables", FOUND_TICKET)

    assert result["downloads"][0]["ok"] is False
    assert result["downloads"][0]["source"] == "geoserver"
    assert "did not finish" in result["downloads"][0]["line"]
    assert "ReadTimeout" in result["downloads"][0]["line"]


def test_a_download_after_the_layer_list_changed_is_refused(monkeypatch):
    fake = Fake([{"action": "download", "id": "E4", "reply": "Downloading."}], version="v2")
    result = run_turn(monkeypatch, fake, "download the first one", FOUND_TICKET)

    assert fake.downloads == []
    assert result["downloads"][0]["ok"] is False
    assert "has changed" in result["downloads"][0]["line"]


def test_a_search_turn_downloads_nothing(monkeypatch):
    fake = Fake([
        {"action": "search", "query": "wind farms"},
        {"action": "answer", "operations": [], "layers": ["E4"], "reply": "I found wind farms on EMODnet."},
    ])
    result = run_turn(monkeypatch, fake, "find wind farms", {})

    assert fake.downloads == []
    assert result["downloads"] == []
    assert result["reply"] == "I found wind farms on EMODnet."
    assert result["picks"]["emodnet"][0]["name"] == "windfarmspoly"


def load_interface_app():
    """The interface's app.py, under its own name, because the search service also has an app.py."""
    spec = importlib.util.spec_from_file_location("interface_app", HERE / "app.py")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def test_a_download_that_also_changes_the_map_does_both(monkeypatch):
    fake = Fake([{"action": "download", "id": "E4", "operations": [{"action": "hide", "layers": ["bathymetry"]}],
                  "reply": "Hiding it and downloading."}])
    result = run_turn(monkeypatch, fake, "hide bathymetry and download the first one", FOUND_TICKET)

    assert result["operations"] == [{"action": "hide", "layers": ["Bathymetry"]}]
    assert result["downloads"][0]["ok"] is True
    assert result["reply"] == result["downloads"][0]["line"]


def test_the_next_sentence_tells_the_model_how_the_download_went(monkeypatch):
    fake = Fake([{"action": "download", "id": "E4", "reply": ""}])
    first = run_turn(monkeypatch, fake, "download the first one", FOUND_TICKET)
    line = first["downloads"][0]["line"]
    assert first["conversation"]["last_download"] == line

    fake = Fake([{"action": "answer", "operations": [], "layers": [], "reply": "It gave 48 shapes."}])
    second = run_turn(monkeypatch, fake, "did that work?", first["conversation"])
    assert fake.messages[0].startswith("Your last download gave: " + line + "\n")
    assert "last_download" not in second["conversation"]


def test_a_turn_that_never_gives_a_step_still_says_something(monkeypatch):
    fake = Fake([])
    replies = []
    for _ in range(4):
        replies.append({"not": "a step"})
    fake.model_replies = replies
    result = run_turn(monkeypatch, fake, "hide bathymetry", FOUND_TICKET)

    assert result["reply"] == conversation.NO_STEP_REPLY
    assert result["downloads"] == []


def test_downloads_of_unfound_ids_until_the_step_limit_say_so(monkeypatch):
    replies = []
    for _ in range(conversation.MAX_STEPS_PER_TURN):
        replies.append({"action": "download", "id": "E9999", "reply": ""})
    fake = Fake(replies)
    result = run_turn(monkeypatch, fake, "download layer E9999", FOUND_TICKET)

    assert fake.downloads == []
    assert result["problems"][-1] == "the model kept asking to download layers no search found, and never answered"
    assert result["reply"] == conversation.NO_STEP_REPLY


def test_a_failed_lookup_of_the_id_is_a_result_and_not_an_error(monkeypatch):
    fake = Fake([{"action": "download", "id": "E4", "reply": ""}], lookup_status=503)
    result = run_turn(monkeypatch, fake, "download the first one", FOUND_TICKET)

    assert fake.downloads == []
    assert result["downloads"][0]["ok"] is False
    assert result["downloads"][0]["source"] == "emodnet"
    assert "could not look it up" in result["downloads"][0]["line"]


def test_an_id_the_catalogue_does_not_have_is_not_called_a_changed_list(monkeypatch):
    ticket = {"chat_key": "k1", "found": [{"id": "G99", "meaning": 0.5}], "version": "v1"}
    fake = Fake([{"action": "download", "id": "G99", "reply": ""}])
    result = run_turn(monkeypatch, fake, "download the first one", ticket)

    assert result["downloads"][0]["ok"] is False
    assert result["downloads"][0]["source"] == "geoserver"
    assert result["downloads"][0]["line"] == "Not downloaded. The layer list has no layer G99. Search again. (team GeoServer)"


def test_a_layer_list_change_still_names_the_server(monkeypatch):
    fake = Fake([{"action": "download", "id": "E4", "reply": ""}], version="v2")
    result = run_turn(monkeypatch, fake, "download the first one", FOUND_TICKET)
    assert result["downloads"][0]["source"] == "emodnet"


def test_an_answer_without_its_counts_is_a_result_and_not_an_error(monkeypatch):
    fake = Fake([{"action": "download", "id": "E4", "reply": ""}], download_answer={"kind": "shapes"})
    result = run_turn(monkeypatch, fake, "download the first one", FOUND_TICKET)

    assert result["downloads"][0]["ok"] is False
    assert "came back without its counts" in result["downloads"][0]["line"]


def test_a_download_slower_than_the_deadline_is_stopped(monkeypatch):
    fake = Fake([{"action": "download", "id": "E4", "reply": ""}])

    async def slow_answer(request: httpx.Request) -> httpx.Response:
        if request.url.path == "/download":
            await asyncio.sleep(1)
        return fake.answer(request)

    def fake_client(**kwargs):
        return REAL_CLIENT(transport=httpx.MockTransport(slow_answer), **kwargs)

    monkeypatch.setattr(conversation, "DOWNLOAD_DEADLINE", 0.1)
    monkeypatch.setattr(conversation.httpx, "AsyncClient", fake_client)
    monkeypatch.setattr(conversation, "VOICE_PROMPT_PATH", str(HERE / "voice-prompt.json"))
    result = asyncio.run(conversation.turn("download the first one", ["Bathymetry"], FOUND_TICKET, None))

    assert result["downloads"][0]["ok"] is False
    assert "took longer than 0.1 seconds" in result["downloads"][0]["line"]


def test_a_bad_region_refuses_only_the_download(monkeypatch):
    fake = Fake([{"action": "download", "id": "E4", "operations": [{"action": "hide", "layers": ["Bathymetry"]}],
                  "reply": ""}])

    def fake_client(**kwargs):
        return REAL_CLIENT(transport=httpx.MockTransport(fake.answer), **kwargs)

    monkeypatch.setattr(conversation.httpx, "AsyncClient", fake_client)
    monkeypatch.setattr(conversation, "VOICE_PROMPT_PATH", str(HERE / "voice-prompt.json"))
    result = asyncio.run(conversation.turn("hide bathymetry and download the first one", ["Bathymetry"],
                                           FOUND_TICKET, None, "region must be 4 numbers"))

    assert fake.downloads == []
    assert result["operations"] == [{"action": "hide", "layers": ["Bathymetry"]}]
    assert result["downloads"][0]["ok"] is False
    assert "region the headset sent is unreadable" in result["downloads"][0]["line"]


def test_voice_does_not_refuse_a_sentence_for_a_bad_region(monkeypatch):
    app_module = load_interface_app()
    calls = []

    async def fake_turn(sentence, map_layers, ticket, region, region_problem):
        calls.append({"region": region, "problem": region_problem})
        return {"operations": [], "picks": {"geoserver": [], "emodnet": []}, "reply": "", "downloads": [],
                "searches": [], "conversation": ticket, "problems": []}

    monkeypatch.setattr(app_module.conversation, "turn", fake_turn)
    client = TestClient(app_module.app)
    for region in ("1,2,3", "nan,1,2,3", "10,0,5,5", "a,b,c,d"):
        response = client.post("/voice", data={"text": "hide bathymetry", "region": region})
        assert response.status_code == 200
        assert calls[-1]["region"] is None
        assert "region must be" in calls[-1]["problem"]

    response = client.post("/voice", data={"text": "hide bathymetry", "region": "1,2,3,4"})
    assert response.status_code == 200
    assert calls[-1] == {"region": [1.0, 2.0, 3.0, 4.0], "problem": ""}


def test_the_download_time_is_not_counted_as_the_model_time(monkeypatch):
    app_module = load_interface_app()

    async def fake_turn(sentence, map_layers, ticket, region, region_problem):
        await asyncio.sleep(0.3)
        download = conversation.download_result(WIND_FARMS, 200, SHAPES_ANSWER, 250)
        return {"operations": [], "picks": {"geoserver": [], "emodnet": []}, "reply": download["line"],
                "downloads": [download], "searches": [], "conversation": ticket, "problems": []}

    monkeypatch.setattr(app_module.conversation, "turn", fake_turn)
    response = TestClient(app_module.app).post("/voice", data={"text": "download the first one"})
    timings = response.json()["timings_ms"]
    assert timings["download"] == 250
    assert 0 <= timings["llm"] < 250

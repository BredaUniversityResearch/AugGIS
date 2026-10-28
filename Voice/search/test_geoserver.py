"""Tests for geoserver.py, which downloads a team GeoServer layer with its login.

    python -m pytest Voice/search

The answers in test_data/geoserver_*.* were saved from the team GeoServer on 2026-10-27 and
hold no login. A fake server (httpx.MockTransport) answers from them, so no server is asked;
it also records every request, so the tests can check what would have been sent.
"""

import asyncio
import base64
import io
import json
from pathlib import Path

import httpx
import pytest
from PIL import Image

import download
import geoserver

DATA = Path(__file__).parent / "test_data"
REGION = [3797132.0, 3137432.0, 3965925.0, 3291676.0]
FAKE_LOGIN = ("test-user", "test-password")

WIND_FARMS = {"source": "geoserver", "name": "northsee:NS_Wind_Farms_Implemented",
              "spoken": "Wind Farms Implemented", "kind": "unknown"}
TELECOM = {"source": "geoserver", "name": "northsee:NS_Telecom_Cables_Emodnet_2026",
           "spoken": "Telecom Cables 2026", "kind": "unknown"}
BATHYMETRY = {"source": "geoserver", "name": "northsee:NS_Bathymetry_Raster", "spoken": "NS Bathymetry Raster",
              "kind": "unknown"}


def saved_text(name: str) -> str:
    return (DATA / name).read_text(encoding="utf-8")


def red_square_png() -> bytes:
    image = Image.new("RGBA", (8, 8), (0, 0, 0, 0))
    image.putpixel((3, 3), (255, 0, 0, 255))
    data = io.BytesIO()
    image.save(data, format="PNG")
    return data.getvalue()


class FakeGeoServer:
    """Answers DescribeLayer, GetFeature and GetMap from saved answers."""

    def __init__(self, describe: str, shapes: str = "geoserver_windfarms.json", status: int = 200):
        self.describe = describe
        self.shapes = shapes
        self.status = status
        self.requests: list[httpx.Request] = []

    def answer(self, request: httpx.Request) -> httpx.Response:
        self.requests.append(request)
        if self.status != 200:
            return httpx.Response(self.status, text="<html>Unauthorized</html>")
        asked = request.url.params.get("request")
        if asked == "DescribeLayer":
            return httpx.Response(200, text=saved_text(self.describe))
        if asked == "GetFeature":
            return httpx.Response(200, text=saved_text(self.shapes), headers={"content-type": "application/json"})
        if asked == "GetMap":
            return httpx.Response(200, content=red_square_png(), headers={"content-type": "image/png"})
        return httpx.Response(400, text="unexpected request")

    def asked(self, kind: str) -> list[httpx.Request]:
        found = []
        for request in self.requests:
            if request.url.params.get("request") == kind:
                found.append(request)
        return found


def use_fake(monkeypatch, fake: FakeGeoServer) -> None:
    """Point geoserver.py at the fake server and give it a login."""
    monkeypatch.setenv("GEOSERVER_USER", FAKE_LOGIN[0])
    monkeypatch.setenv("GEOSERVER_PASSWORD", FAKE_LOGIN[1])

    def fake_client(auth):
        return httpx.AsyncClient(transport=httpx.MockTransport(fake.answer), auth=auth)

    monkeypatch.setattr(geoserver, "make_client", fake_client)


def refused(job) -> download.Refused:
    with pytest.raises(download.Refused) as caught:
        asyncio.run(job)
    return caught.value


def test_a_missing_login_is_refused_before_anything_is_asked(monkeypatch):
    fake = FakeGeoServer("geoserver_describe_windfarms.json")
    use_fake(monkeypatch, fake)
    monkeypatch.delenv("GEOSERVER_PASSWORD")
    answer = refused(geoserver.download_layer(WIND_FARMS, REGION, False))
    assert answer.status == 503
    assert answer.message == geoserver.NO_LOGIN
    assert fake.requests == []


def test_the_login_goes_with_every_request_and_only_to_the_geoserver(monkeypatch):
    fake = FakeGeoServer("geoserver_describe_windfarms.json")
    use_fake(monkeypatch, fake)
    asyncio.run(geoserver.download_layer(WIND_FARMS, REGION, False))
    expected = "Basic " + base64.b64encode(":".join(FAKE_LOGIN).encode()).decode()
    assert len(fake.requests) == 2
    for request in fake.requests:
        assert request.headers["authorization"] == expected
        assert request.url.host == "geo.mspchallenge.info"


def test_a_refused_login_says_where_the_login_is_set(monkeypatch):
    use_fake(monkeypatch, FakeGeoServer("geoserver_describe_windfarms.json", status=401))
    answer = refused(geoserver.download_layer(WIND_FARMS, REGION, False))
    assert answer.status == 502
    assert answer.message == download.LOGIN_REFUSED
    assert "Voice/.env" in answer.message


def test_a_layer_the_geoserver_does_not_have_is_refused_plainly(monkeypatch):
    use_fake(monkeypatch, FakeGeoServer("geoserver_no_such_layer.xml"))
    layer = {"source": "geoserver", "name": "northsee:NS_Pipelines_DT", "spoken": "Pipelines DT"}
    answer = refused(geoserver.download_layer(layer, REGION, False))
    assert answer.status == 404
    assert answer.message.startswith("The team GeoServer says it has no layer northsee:NS_Pipelines_DT (Pipelines DT).")
    assert "read-only login may not read it" in answer.message


def test_kind_of_reads_the_describe_answer_the_way_msp_does():
    def descriptions(name):
        return json.loads(saved_text(name))["layerDescriptions"]

    assert geoserver.kind_of(descriptions("geoserver_describe_windfarms.json")) == "shapes"
    assert geoserver.kind_of(descriptions("geoserver_describe_bathymetry.json")) == "picture"
    assert geoserver.kind_of(descriptions("geoserver_describe_passed_on.json")) == "picture"
    assert geoserver.kind_of(descriptions("geoserver_describe_telecom_group.json")) == "group"
    assert geoserver.served_as(descriptions("geoserver_describe_passed_on.json")) == "WMS"
    assert geoserver.served_as(descriptions("geoserver_describe_telecom_group.json")) == "group of 4"


def test_a_shape_layer_comes_back_as_the_geojson_the_geoserver_sent(monkeypatch):
    fake = FakeGeoServer("geoserver_describe_windfarms.json")
    use_fake(monkeypatch, fake)
    result = asyncio.run(geoserver.download_layer(WIND_FARMS, REGION, False))
    sent = json.loads(saved_text("geoserver_windfarms.json"))
    assert result["kind"] == "shapes"
    assert result["served_as"] == "WFS"
    assert result["title"] == "Wind Farms Implemented"
    assert result["geojson"] == sent
    assert result["features"] == len(sent["features"])
    assert "describe" in result["timings_ms"]


def test_the_shape_request_asks_for_epsg_3035_with_the_region_easting_first(monkeypatch):
    fake = FakeGeoServer("geoserver_describe_windfarms.json")
    use_fake(monkeypatch, fake)
    asyncio.run(geoserver.download_layer(WIND_FARMS, REGION, False))
    shapes = fake.asked("GetFeature")[0]
    assert shapes.url.path == "/geoserver/wfs"
    assert shapes.url.params["typeNames"] == "northsee:NS_Wind_Farms_Implemented"
    assert shapes.url.params["srsName"] == "EPSG:3035"
    assert shapes.url.params["bbox"] == "3797132.00,3137432.00,3965925.00,3291676.00,EPSG:3035"


def test_a_group_comes_back_as_the_picture_the_geoserver_draws_of_it(monkeypatch):
    fake = FakeGeoServer("geoserver_describe_telecom_group.json")
    use_fake(monkeypatch, fake)
    result = asyncio.run(geoserver.download_layer(TELECOM, REGION, False))
    assert result["kind"] == "picture"
    assert result["served_as"] == "group of 4"
    assert fake.asked("GetFeature") == []
    assert len(fake.asked("GetMap")) == 1


def test_a_raster_comes_back_as_a_picture(monkeypatch):
    fake = FakeGeoServer("geoserver_describe_bathymetry.json")
    use_fake(monkeypatch, fake)
    result = asyncio.run(geoserver.download_layer(BATHYMETRY, REGION, False))
    assert result["kind"] == "picture"
    assert result["served_as"] == "WCS"
    assert result["title"] == "NS Bathymetry Raster"
    assert base64.b64decode(result["png_base64"]) == red_square_png()
    picture_request = fake.asked("GetMap")[0]
    assert picture_request.url.path == "/geoserver/wms"
    assert picture_request.url.params["srs"] == "EPSG:3035"
    assert picture_request.url.params["bbox"] == "3797132.00,3137432.00,3965925.00,3291676.00"


def test_as_picture_draws_a_shape_layer_instead(monkeypatch):
    fake = FakeGeoServer("geoserver_describe_windfarms.json")
    use_fake(monkeypatch, fake)
    result = asyncio.run(geoserver.download_layer(WIND_FARMS, REGION, True))
    assert result["kind"] == "picture"
    assert result["served_as"] == "WFS"
    assert fake.asked("GetFeature") == []

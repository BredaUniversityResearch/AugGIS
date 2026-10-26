"""Tests for download.py, which downloads EMODnet shapes as the GeoJSON the server sends.

    python -m pytest Voice/search

A fake server (httpx.MockTransport) answers from the saved answers in test_data/, so no
server is asked and they run without the containers.
"""

import asyncio
import json
from pathlib import Path

import httpx
import pytest

import download

DATA = Path(__file__).parent / "test_data"
REGION = [3797132.0, 3137432.0, 3965925.0, 3291676.0]

WIND_FARMS = {"source": "emodnet", "name": "windfarmspoly", "spoken": "Wind Farms (Polygons)", "kind": "shapes",
              "url": "https://ows.emodnet-humanactivities.eu/wms",
              "wfs_url": "https://ows.emodnet-humanactivities.eu/wfs", "wfs_name": "emodnet:windfarmspoly"}


def saved(name: str) -> dict:
    return json.loads((DATA / name).read_text(encoding="utf-8"))


def fake_client(collection: dict, status: int = 200, requests: list | None = None) -> httpx.AsyncClient:
    """A client whose every GetFeature is answered with this collection."""

    def answer(request: httpx.Request) -> httpx.Response:
        if requests is not None:
            requests.append(request)
        if status != 200:
            return httpx.Response(status, text="<html>Unauthorized</html>")
        return httpx.Response(200, json=collection)

    return httpx.AsyncClient(transport=httpx.MockTransport(answer))


def shapes(collection: dict, status: int = 200, requests: list | None = None) -> dict:
    async def job():
        async with fake_client(collection, status, requests) as client:
            return await download.download_shapes(client, WIND_FARMS, WIND_FARMS["spoken"], REGION, 0.0)

    return asyncio.run(job())


def test_the_geojson_comes_back_as_the_server_sent_it():
    collection = saved("polygons.json")
    result = shapes(collection)
    assert result["kind"] == "shapes"
    assert result["title"] == "Wind Farms (Polygons)"
    assert result["geojson"] == collection
    assert result["features"] == 2
    assert result["held_back"] == ""


def test_the_shape_request_asks_for_epsg_3035_with_the_region_easting_first():
    requests = []
    shapes(saved("polygons.json"), requests=requests)
    asked = requests[0]
    assert asked.url.path == "/wfs"
    assert asked.url.params["typeNames"] == "emodnet:windfarmspoly"
    assert asked.url.params["srsName"] == "EPSG:3035"
    assert asked.url.params["bbox"] == "3797132.00,3137432.00,3965925.00,3291676.00,EPSG:3035"


def test_nothing_in_the_region_is_refused():
    with pytest.raises(download.Refused) as refused:
        shapes({"type": "FeatureCollection", "features": []})
    assert refused.value.status == 422
    assert refused.value.message == "Nothing from Wind Farms (Polygons) lies in this region."


def test_a_refused_login_says_where_the_login_is_set():
    with pytest.raises(download.Refused) as refused:
        shapes(saved("polygons.json"), status=401)
    assert refused.value.status == 502
    assert refused.value.message == download.LOGIN_REFUSED


def test_a_layer_that_is_not_shapes_is_refused_before_anything_is_asked():
    for kind, words in (("picture", "is a picture, not a shape layer"), ("unknown", "did not list its shapes")):
        layer = dict(WIND_FARMS, kind=kind)
        with pytest.raises(download.Refused) as refused:
            asyncio.run(download.download(layer, REGION))
        assert refused.value.status == 422
        assert words in refused.value.message


def test_a_team_geoserver_layer_is_refused_here():
    layer = dict(WIND_FARMS, source="geoserver")
    with pytest.raises(download.Refused) as refused:
        asyncio.run(download.download(layer, REGION))
    assert refused.value.status == 422
    assert "geoserver.download_layer" in refused.value.message


def test_features_of_is_empty_for_a_collection_without_a_list():
    assert download.features_of({}) == []
    assert download.features_of({"features": None}) == []


def test_a_geoserver_error_keeps_only_its_reason():
    body = """<?xml version="1.0" encoding="UTF-8"?><ows:ExceptionReport xmlns:ows="http://www.opengis.net/ows/1.1" version="2.0.0">
  <ows:Exception exceptionCode="InvalidParameterValue" locator="bbox">
    <ows:ExceptionText>Requested bounding box contains wrong
      number of coordinates</ows:ExceptionText>
  </ows:Exception></ows:ExceptionReport>"""
    assert download.server_reason(body) == ": Requested bounding box contains wrong number of coordinates."
    assert download.server_reason("<html>Bad gateway</html>") == "."
    java = "<ows:ExceptionText>java.lang.IllegalArgumentException: Unknown layer</ows:ExceptionText>"
    assert download.server_reason(java) == ": Unknown layer."


def test_a_geoserver_error_reason_reads_an_ampersand_as_one():
    body = ("<ServiceExceptionReport><ServiceException>Error for URL "
            "https://example.org/ows?REQUEST=GetMap&amp;FORMAT=image/png</ServiceException></ServiceExceptionReport>")
    assert download.server_reason(body) == ": Error for URL https://example.org/ows?REQUEST=GetMap&FORMAT=image/png."


def test_held_back_names_the_shapes_the_server_did_not_send():
    assert download.held_back_sentence("Wind Farms", 48, 48) == ""
    assert download.held_back_sentence("Wind Farms", None, 48) == ""
    assert download.held_back_sentence("Wind Farms", "unknown", 48) == ""
    sentence = download.held_back_sentence("Wind Farms", 1200, 1000)
    assert sentence == "Wind Farms: the server has 1200 shapes in this region but sent only 1000, so 200 are missing from the layer."

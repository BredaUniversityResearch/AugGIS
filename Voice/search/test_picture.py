"""Tests for picture.py, which downloads an EMODnet layer as the PNG its WMS draws.

    python -m pytest Voice/search

The pictures are made here with Pillow, so no server is asked.
"""

import asyncio
import base64
import io

import pytest
from PIL import Image

import download
import picture

REGION = [3797132.0, 3137432.0, 3965925.0, 3291676.0]


def png(image: Image.Image) -> bytes:
    data = io.BytesIO()
    image.save(data, format="PNG")
    return data.getvalue()


def test_the_picture_keeps_the_regions_shape_with_a_long_edge_of_1024():
    assert picture.picture_size(REGION) == (1024, 936)
    assert picture.picture_size([0, 0, 100, 200]) == (512, 1024)


def test_a_fully_see_through_picture_is_empty():
    assert picture.is_empty(png(Image.new("RGBA", (8, 8), (0, 0, 0, 0))))


def test_one_visible_pixel_is_not_empty():
    image = Image.new("RGBA", (8, 8), (0, 0, 0, 0))
    image.putpixel((3, 4), (200, 30, 30, 255))
    assert not picture.is_empty(png(image))


def test_a_picture_without_see_through_pixels_is_not_empty():
    assert not picture.is_empty(png(Image.new("RGB", (8, 8), (255, 255, 255))))
    palette = Image.new("P", (8, 8), 0)
    assert not picture.is_empty(png(palette))


def test_a_wms_1_1_1_error_keeps_only_its_reason():
    body = """<?xml version="1.0" encoding="UTF-8"?><ServiceExceptionReport version="1.1.1">
  <ServiceException code="LayerNotDefined" locator="layers">
      Could not find layer nothing
  </ServiceException></ServiceExceptionReport>"""
    assert download.server_reason(body) == ": Could not find layer nothing."


def fake_picture(monkeypatch, image: Image.Image) -> None:
    """Answer picture.download_picture's GetMap from a picture made here, so no server is asked."""

    async def fake_get_picture(client, layer, title, region, width, height):
        return png(image)

    monkeypatch.setattr(picture, "get_picture", fake_get_picture)


VESSELS = {"source": "emodnet", "name": "vesseldensity_allavg", "spoken": "Vessel Density", "kind": "picture",
           "url": "https://ows.emodnet-humanactivities.eu/wms"}


def test_download_picture_returns_the_png_as_base64(monkeypatch):
    image = Image.new("RGBA", (8, 8), (0, 0, 0, 0))
    image.putpixel((2, 2), (255, 0, 0, 255))
    fake_picture(monkeypatch, image)
    result = asyncio.run(picture.download_picture(VESSELS, REGION))
    assert result["kind"] == "picture"
    assert result["title"] == "Vessel Density"
    assert (result["width"], result["height"]) == (1024, 936)
    assert base64.b64decode(result["png_base64"]) == png(image)
    assert result["bytes"] == len(png(image))


def test_download_picture_refuses_a_fully_see_through_picture(monkeypatch):
    fake_picture(monkeypatch, Image.new("RGBA", (8, 8), (0, 0, 0, 0)))
    with pytest.raises(download.Refused) as refused:
        asyncio.run(picture.download_picture(VESSELS, REGION))
    assert refused.value.status == 422
    assert refused.value.message.startswith("Nothing from Vessel Density lies in this region: its picture is empty.")


def test_download_picture_refuses_the_team_geoserver(monkeypatch):
    fake_picture(monkeypatch, Image.new("RGBA", (8, 8), (255, 0, 0, 255)))
    geoserver = dict(VESSELS, source="geoserver")
    with pytest.raises(download.Refused) as refused:
        asyncio.run(picture.download_picture(geoserver, REGION))
    assert refused.value.status == 422

"""Tests for how catalogue.py tells shape layers, layer groups and pictures apart.

    python -m pytest Voice/search

The XML is cut down from EMODnet Human Activities' real WMS capabilities, so these run
without the containers or the internet.
"""

import json
import xml.etree.ElementTree as ElementTree

import catalogue

SHAPE_NAMES = ["emodnet:windfarmspoly", "emodnet:pipelines", "tno:sediment", "bgs:sediment"]
BY_BARE_NAME = catalogue.names_without_workspace(SHAPE_NAMES)

WIND_FARMS = """<Layer xmlns="http://www.opengis.net/wms" queryable="1">
  <Name>windfarmspoly</Name><Title>Wind Farms (Polygons)</Title>
  <Style><Name>windfarmspoly</Name><Title>windfarmspoly</Title></Style>
</Layer>"""

POWER_CABLES = """<Layer xmlns="http://www.opengis.net/wms" queryable="1">
  <Name>powercables</Name><Title>Power Cables</Title>
  <Style><Name>default-style-powercables</Name><Title>powercables style</Title></Style>
</Layer>"""

NESTED_GROUP = """<Layer xmlns="http://www.opengis.net/wms">
  <Name>bgs:submerged_landscapes</Name><Title>Submerged landscapes</Title>
  <Layer><Name>bgs:peat</Name><Title>Peat</Title></Layer>
</Layer>"""


def test_wfs_address_swaps_the_last_part():
    assert catalogue.wfs_address("https://ows.emodnet-humanactivities.eu/wms") == "https://ows.emodnet-humanactivities.eu/wfs"
    assert (catalogue.wfs_address("https://geo.vliz.be/geoserver/Emodnetbio/wms")
            == "https://geo.vliz.be/geoserver/Emodnetbio/wfs")


def test_shape_twin_matches_a_name_without_its_workspace():
    assert catalogue.shape_twin("windfarmspoly", SHAPE_NAMES, BY_BARE_NAME) == "emodnet:windfarmspoly"


def test_shape_twin_matches_the_same_name_exactly():
    assert catalogue.shape_twin("tno:sediment", SHAPE_NAMES, BY_BARE_NAME) == "tno:sediment"


def test_shape_twin_does_not_guess_between_two_workspaces():
    assert catalogue.shape_twin("sediment", SHAPE_NAMES, BY_BARE_NAME) is None


def test_shape_twin_is_none_for_a_picture_layer():
    assert catalogue.shape_twin("routedensity_01", SHAPE_NAMES, BY_BARE_NAME) is None


def test_a_group_has_the_default_style_named_after_it():
    assert catalogue.is_layer_group(ElementTree.fromstring(POWER_CABLES), "powercables")


def test_a_group_can_list_its_members_inside_it():
    assert catalogue.is_layer_group(ElementTree.fromstring(NESTED_GROUP), "bgs:submerged_landscapes")


def test_a_layer_with_its_own_style_is_not_a_group():
    assert not catalogue.is_layer_group(ElementTree.fromstring(WIND_FARMS), "windfarmspoly")


def test_a_saved_list_gets_the_current_server_addresses(tmp_path, monkeypatch):
    old_physics = "https://geoserver.emodnet-physics.eu/geoserver/emodnet/wms"
    saved_list = [{
        "source": "emodnet", "group": "Physics", "name": "EP_PLATFORMS", "kind": "shapes",
        "url": old_physics, "wfs_url": catalogue.wfs_address(old_physics), "wfs_name": "EMODnet:EP_PLATFORMS",
    }]
    cache = tmp_path / "catalogue.json"
    cache.write_text(json.dumps(saved_list), encoding="utf-8")
    monkeypatch.setattr(catalogue, "CACHE", cache)

    layer = catalogue.saved_layers("emodnet", "Physics")[0]
    assert layer["url"] == catalogue.EMODNET_SERVERS["Physics"]
    assert layer["wfs_url"] == "https://prod-geoserver.emodnet-physics.eu/geoserver/EMODnet/wfs"

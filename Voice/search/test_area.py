"""Offline tests for area.py and the layer boxes catalogue.py reads. Run: python -m pytest -q"""

import xml.etree.ElementTree as ElementTree

import area
import catalogue

BELGIAN_COAST = [3797132, 3137432, 3965925, 3291676]


def test_the_belgian_box_in_degrees_is_the_belgian_and_dutch_coast():
    west, south, east, north = area.region_in_degrees(BELGIAN_COAST)
    assert 2.26 < west < 2.28
    assert 4.90 < east < 4.92
    assert 51.10 < south < 51.11
    assert 52.61 < north < 52.62


def test_a_box_over_the_map_is_on_it():
    region_box = area.region_in_degrees(BELGIAN_COAST)
    wind_farms = [-16.59, 24.74, 27.73, 65.47]
    assert area.on_map(wind_farms, region_box) == "yes"


def test_the_usa_and_the_baltic_are_not_on_the_belgian_map():
    region_box = area.region_in_degrees(BELGIAN_COAST)
    usa_states = [-124.73, 24.96, -66.97, 49.37]
    assert area.on_map(usa_states, region_box) == "no"
    assert area.on_map(catalogue.SEA_BOXES["balticline"], region_box) == "no"


def test_a_box_reads_as_compass_degrees():
    assert area.box_words([-124.73, 24.96, -66.97, 49.37]) == "124.7 W to 67.0 W, 25.0 N to 49.4 N"


def test_a_layer_without_a_box_is_unknown():
    region_box = area.region_in_degrees(BELGIAN_COAST)
    assert area.on_map(None, region_box) == "unknown"


def test_the_tile_cache_placeholder_box_counts_as_no_box():
    element = ElementTree.fromstring(
        '<Layer xmlns:ows="http://www.opengis.net/ows/1.1"><ows:WGS84BoundingBox>'
        "<ows:LowerCorner>0.0 0.0</ows:LowerCorner><ows:UpperCorner>-1.0 -1.0</ows:UpperCorner>"
        "</ows:WGS84BoundingBox></Layer>")
    assert catalogue.wmts_box(element) is None


def test_a_tile_cache_box_is_read_longitude_first():
    element = ElementTree.fromstring(
        '<Layer xmlns:ows="http://www.opengis.net/ows/1.1"><ows:WGS84BoundingBox>'
        "<ows:LowerCorner>-124.73 24.96</ows:LowerCorner><ows:UpperCorner>-66.97 49.37</ows:UpperCorner>"
        "</ows:WGS84BoundingBox></Layer>")
    assert catalogue.wmts_box(element) == [-124.73, 24.96, -66.97, 49.37]


def test_a_wms_box_is_read_from_its_four_named_numbers():
    element = ElementTree.fromstring(
        '<Layer xmlns="http://www.opengis.net/wms"><EX_GeographicBoundingBox>'
        "<westBoundLongitude>-16.59</westBoundLongitude><eastBoundLongitude>27.73</eastBoundLongitude>"
        "<southBoundLatitude>24.74</southBoundLatitude><northBoundLatitude>65.47</northBoundLatitude>"
        "</EX_GeographicBoundingBox></Layer>")
    assert catalogue.wms_box(element) == [-16.59, 24.74, 27.73, 65.47]

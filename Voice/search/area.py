"""Whether a layer's area overlaps the map's region.

The map's region comes in EPSG:3035 (metres on a flat map of Europe), while the map servers
give each layer's area in degrees of longitude and latitude (WGS84). So the region is turned
into degrees first. A straight edge in metres is a curve in degrees, so the region's whole
outline is turned, not only its 4 corners, and the box around the result is used.
"""

from pyproj import Transformer

# always_xy: both sides are written easting/longitude first, whatever the official order is.
TO_DEGREES = Transformer.from_crs("EPSG:3035", "EPSG:4326", always_xy=True)

# Points per edge of the region's outline. 10 keeps the box within metres of the real curve.
POINTS_PER_EDGE = 10


def region_in_degrees(region: list[float]) -> list[float]:
    """The box in degrees, [west, south, east, north], around a [minx, miny, maxx, maxy] EPSG:3035 region."""
    minx, miny, maxx, maxy = region
    xs = []
    ys = []
    for step in range(POINTS_PER_EDGE + 1):
        part = step / POINTS_PER_EDGE
        x = minx + part * (maxx - minx)
        y = miny + part * (maxy - miny)
        xs.extend([x, x, minx, maxx])
        ys.extend([miny, maxy, y, y])
    longitudes, latitudes = TO_DEGREES.transform(xs, ys)
    return [min(longitudes), min(latitudes), max(longitudes), max(latitudes)]


def degrees_words(value: float, positive: str, negative: str) -> str:
    """One coordinate as a person reads it: -124.7 with E/W becomes "124.7 W".

    No degree sign: the headset panel's font may not have one, and would show a box instead.
    """
    if value < 0:
        return format(-value, ".1f") + " " + negative
    return format(value, ".1f") + " " + positive


def box_words(box: list[float]) -> str:
    """A [west, south, east, north] box as words: "124.7 W to 67.0 W, 25.0 N to 49.4 N"."""
    west, south, east, north = box
    across = degrees_words(west, "E", "W") + " to " + degrees_words(east, "E", "W")
    up = degrees_words(south, "N", "S") + " to " + degrees_words(north, "N", "S")
    return across + ", " + up


def on_map(box: list[float] | None, region_box: list[float]) -> str:
    """ "yes" when the layer's box overlaps the region's, "no" when it does not, "unknown" without a box.

    A box is a rectangle around the data, so "yes" means the data may be on the map, not that
    it is: a layer can overlap the region and still have nothing inside it.
    """
    if box is None:
        return "unknown"
    west, south, east, north = box
    region_west, region_south, region_east, region_north = region_box
    apart_east_west = east < region_west or west > region_east
    apart_north_south = north < region_south or south > region_north
    if apart_east_west or apart_north_south:
        return "no"
    return "yes"

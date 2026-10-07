"""Tests for checks.py, the gate between the language model's answer and the map.

    python -m pytest Voice/interface

checks.py imports nothing outside Python itself, so these run on any machine without the
containers.
"""

import checks

MAP_LAYERS = ["Population", "Bathymetry", "Shipping lanes", "Wind farms"]

# What Whisper is told to expect in the running service: the map's names, then these.
VOCABULARY = ["bathymetry", "wind farms", "cables", "pipelines", "shipping lanes", "Natura 2000"]
EXPECTED = MAP_LAYERS + VOCABULARY


def test_parse_step_reads_a_search():
    step = checks.parse_step('{"action": "search", "query": " wind farms "}')
    assert step == {"action": "search", "query": "wind farms"}


def test_parse_step_reads_an_answer_wrapped_in_a_code_block():
    reply = 'Here you go:\n```json\n{"action": "answer", "operations": [], "layers": ["G1"], "reply": "Found one."}\n```'
    step = checks.parse_step(reply)
    assert step == {"action": "answer", "operations": [], "layers": ["G1"], "reply": "Found one."}


def test_parse_step_refuses_prose():
    assert checks.parse_step("I have hidden the bathymetry layer for you.") is None


def test_parse_step_refuses_an_unknown_action():
    assert checks.parse_step('{"action": "delete", "layers": ["Bathymetry"]}') is None


def test_parse_step_refuses_a_search_without_words():
    assert checks.parse_step('{"action": "search", "query": ""}') is None


def test_parse_step_replaces_fields_of_the_wrong_type():
    step = checks.parse_step('{"action": "answer", "operations": "hide all", "layers": null, "reply": 5}')
    assert step == {"action": "answer", "operations": [], "layers": [], "reply": ""}


def test_operations_keep_the_maps_own_spelling():
    raw = [{"action": "hide", "layers": ["bathymetry", "SHIPPING LANES"]}]
    assert checks.valid_operations(raw, MAP_LAYERS) == [{"action": "hide", "layers": ["Bathymetry", "Shipping lanes"]}]


def test_operations_drop_a_layer_that_is_not_on_the_map():
    raw = [{"action": "show", "layers": ["Seagrass", "Population"]}]
    assert checks.valid_operations(raw, MAP_LAYERS) == [{"action": "show", "layers": ["Population"]}]


def test_operations_drop_an_operation_left_with_no_layers():
    raw = [{"action": "show", "layers": ["Seagrass"]}, {"action": "hide", "layers": ["Wind farms"]}]
    assert checks.valid_operations(raw, MAP_LAYERS) == [{"action": "hide", "layers": ["Wind farms"]}]


def test_operations_drop_an_action_that_is_not_show_hide_or_toggle():
    raw = [{"action": "delete", "layers": ["Population"]}, {"action": "none", "layers": ["Population"]}]
    assert checks.valid_operations(raw, MAP_LAYERS) == []


def test_operations_accept_toggle_and_capitals_in_the_action():
    raw = [{"action": "Toggle", "layers": [" Wind farms "]}]
    assert checks.valid_operations(raw, MAP_LAYERS) == [{"action": "toggle", "layers": ["Wind farms"]}]


def test_operations_collapse_a_layer_named_twice():
    raw = [{"action": "hide", "layers": ["Wind farms", "Wind farms"]}]
    assert checks.valid_operations(raw, MAP_LAYERS) == [{"action": "hide", "layers": ["Wind farms"]}]


def test_operations_survive_entries_of_the_wrong_shape():
    raw = ["hide everything", {"action": "hide"}, {"action": "hide", "layers": [3, None, "Population"]}]
    assert checks.valid_operations(raw, MAP_LAYERS) == [{"action": "hide", "layers": ["Population"]}]


def test_picks_keep_only_ids_a_search_returned():
    assert checks.valid_picks(["G1", "G999", "E4"], ["G1", "G2", "E4"]) == ["G1", "E4"]


def test_picks_keep_each_id_once():
    assert checks.valid_picks(["G1", "G1", "G2"], ["G1", "G2"]) == ["G1", "G2"]


def test_picks_keep_at_most_ten():
    found = ["G" + str(number) for number in range(20)]
    assert checks.valid_picks(found, found) == found[:10]


def test_echo_is_the_expected_names_recited_in_order():
    assert checks.is_list_echo("Population, Bathymetry, Shipping lanes and Wind farms.", EXPECTED)


def test_echo_can_start_partway_through_the_list():
    assert checks.is_list_echo("Bathymetry, shipping lanes, wind farms", EXPECTED)


def test_one_name_said_alone_is_speech():
    assert not checks.is_list_echo("Bathymetry", EXPECTED)


def test_two_names_said_alone_are_speech():
    assert not checks.is_list_echo("Bathymetry and wind farms", EXPECTED)


def test_three_names_in_the_speakers_own_order_are_speech():
    assert not checks.is_list_echo("Wind farms, population and bathymetry", EXPECTED)


def test_three_names_that_are_not_neighbours_in_the_list_are_speech():
    assert not checks.is_list_echo("Cables, pipelines and Natura 2000", EXPECTED)


def test_a_sentence_with_a_verb_is_speech():
    assert not checks.is_list_echo("Hide population, bathymetry and shipping lanes", EXPECTED)


def test_an_empty_transcript_is_not_an_echo():
    assert not checks.is_list_echo("", EXPECTED)

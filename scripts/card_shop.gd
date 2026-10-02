extends Control

signal turn_confirmed
signal hand_changed(count: int)
signal next_round_requested

const CARD_SCRIPT = preload("res://scripts/shop_card.gd")
const HAND_SCRIPT = preload("res://scripts/card_hand_zone.gd")
const FIELD_SCRIPT = preload("res://scripts/card_field_zone.gd")
const SHOP_ZONE_SCRIPT = preload("res://scripts/card_shop_zone.gd")
const UNIT_SCENE = preload("res://scenes/Character.tscn")
const CARD_SHEET = preload("res://assets/cards/pixelCardAssest_V01.png")
const SHOP_LIMIT: int = 4
const HAND_LIMIT: int = 10
const FIELD_LIMIT: int = 6
const DEPLOYMENT_CELLS: Array[Vector2i] = [Vector2i(44, 14), Vector2i(44, 18), Vector2i(44, 22), Vector2i(48, 14), Vector2i(48, 18), Vector2i(48, 22)]

var pool: Array[Dictionary] = []
var offers: Array[Dictionary] = []
var hand: Array[Dictionary] = []
var drafting: bool = false
var turn_number: int = 0
var _serial: int = 0
var _rng := RandomNumberGenerator.new()
var _shop_row: HBoxContainer
var _hand_row: HBoxContainer
var _shop_label: Label
var _hand_label: Label
var _next_button: TextureButton
var _notice: Label
var field: Node2D
var deployed: Dictionary = {}
var _player_material: Material
var _finished: bool = false
var _deployment_mode: bool = false
var _details: NinePatchRect
var _details_text: Label
var _camera_position: Vector2
var _active_drag: Dictionary = {}
var _inspected_unit: Node2D

func _process(_delta: float) -> void:
	if _details != null and _details.visible and is_instance_valid(_inspected_unit):
		show_unit_details(_inspected_unit)


func _notification(what: int) -> void:
	if what == NOTIFICATION_DRAG_BEGIN:
		var data: Variant = get_viewport().gui_get_drag_data()
		_active_drag = data.duplicate() if data is Dictionary and data.get("card_shop") == self else {}
	elif what == NOTIFICATION_DRAG_END:
		if not _active_drag.is_empty() and not get_viewport().gui_is_drag_successful():
			_finish_loose_drop.call_deferred(_active_drag, get_global_mouse_position())
		_active_drag = {}


func _finish_loose_drop(data: Dictionary, ui_position: Vector2) -> void:
	if not drafting or data.has("unit"):
		return
	if data.get("source") == "shop":
		if not get_node("ZoneA").get_global_rect().has_point(ui_position):
			take_card(data.token)
	elif data.get("source") == "hand" and not get_node("ZoneB").get_global_rect().has_point(ui_position):
		if get_node("ZoneA").get_global_rect().has_point(ui_position):
			sell_card(data.token)
			return
		var world_position := get_viewport().get_canvas_transform().affine_inverse() * ui_position
		var slot := auto_place_slot(data, world_position)
		if slot >= 0:
			var tiles: TileMapLayer = field.get_node("TileMapLayer")
			place_card_or_unit(data, tiles.to_global(tiles.map_to_local(DEPLOYMENT_CELLS[slot])))


func auto_place_slot(data: Dictionary, world_position: Vector2) -> int:
	var tiles: TileMapLayer = field.get_node("TileMapLayer")
	var closest := -1
	var distance := INF
	for i in range(DEPLOYMENT_CELLS.size()):
		var center := tiles.to_global(tiles.map_to_local(DEPLOYMENT_CELLS[i]))
		if can_place(data, center) and center.distance_squared_to(world_position) < distance:
			closest = i
			distance = center.distance_squared_to(world_position)
	return closest


func _ready() -> void:
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
	_rng.randomize()
	field = get_parent().get_parent().get_parent().get_parent()
	var battle := field.get_node("BattleDemo")
	_deployment_mode = battle.card_shop_enabled and battle.auto_start
	if _deployment_mode:
		# Replace the demonstration allies with units chosen from the hand.
		for child in field.get_children():
			if child.get_meta("team", "") == "Ally":
				_player_material = child.get_node("AnimatedSprite2D").material
				child.hide()
				child.queue_free()
	# Original complete card faces from the user's sheet. No recoloring or new art.
	var names := ["Blue", "Red", "Silver", "Green", "Gold", "Stone"]
	var origins := [14, 133, 250, 367, 482, 611]
	for i in range(origins.size()):
		pool.append({"id": i, "name": names[i], "texture": _atlas(Rect2(origins[i], 4, 100, 128))})
	_build_ui()
	_refresh()


func _atlas(rect: Rect2) -> AtlasTexture:
	var texture := AtlasTexture.new()
	texture.atlas = CARD_SHEET
	texture.region = rect
	return texture


func _build_ui() -> void:
	if _deployment_mode:
		var field_zone := Control.new()
		field_zone.name = "FieldDrop"
		field_zone.set_script(FIELD_SCRIPT)
		field_zone.shop = self
		add_child(field_zone)
		field_zone.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
		field_zone.mouse_filter = Control.MOUSE_FILTER_STOP
	var shop_zone := Control.new()
	shop_zone.name = "ZoneA"
	shop_zone.set_script(SHOP_ZONE_SCRIPT)
	shop_zone.shop = self
	shop_zone.anchor_left = 0.5
	shop_zone.anchor_right = 0.5
	shop_zone.offset_left = -185
	shop_zone.offset_right = 185
	shop_zone.offset_bottom = 167
	shop_zone.mouse_filter = Control.MOUSE_FILTER_STOP
	add_child(shop_zone)
	_shop_label = Label.new()
	_shop_label.size = Vector2(370, 24)
	_shop_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_shop_label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	shop_zone.add_child(_shop_label)
	_shop_row = HBoxContainer.new()
	_shop_row.position = Vector2(20, 28)
	_shop_row.size = Vector2(330, 100)
	_shop_row.alignment = BoxContainer.ALIGNMENT_CENTER
	shop_zone.add_child(_shop_row)
	_next_button = TextureButton.new()
	_next_button.position = Vector2(20, 137)
	_next_button.size = Vector2(160, 30)
	_next_button.ignore_texture_size = true
	_next_button.texture_normal = _atlas(Rect2(16, 223, 96, 29))
	_next_button.stretch_mode = TextureButton.STRETCH_SCALE
	_next_button.pressed.connect(confirm_turn)
	shop_zone.add_child(_next_button)
	var next_label := Label.new()
	next_label.name = "Text"
	next_label.text = "BATTLE"
	next_label.modulate = Color.BLACK
	next_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	next_label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	next_label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_next_button.add_child(next_label)
	next_label.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	_notice = Label.new()
	_notice.position = Vector2(190, 140)
	_notice.mouse_filter = Control.MOUSE_FILTER_IGNORE
	shop_zone.add_child(_notice)
	var hand_zone := Control.new()
	hand_zone.name = "ZoneB"
	hand_zone.set_script(HAND_SCRIPT)
	hand_zone.shop = self
	hand_zone.anchor_top = 1.0
	hand_zone.anchor_right = 1.0
	hand_zone.anchor_bottom = 1.0
	hand_zone.offset_top = -125
	add_child(hand_zone)
	_hand_label = Label.new()
	_hand_label.anchor_right = 1.0
	_hand_label.offset_bottom = 24
	_hand_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_hand_label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	hand_zone.add_child(_hand_label)
	_hand_row = HBoxContainer.new()
	_hand_row.anchor_left = 0.5
	_hand_row.anchor_right = 0.5
	_hand_row.offset_left = -338
	_hand_row.offset_right = 338
	_hand_row.offset_top = 26
	_hand_row.offset_bottom = 109
	_hand_row.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_hand_row.alignment = BoxContainer.ALIGNMENT_CENTER
	hand_zone.add_child(_hand_row)
	_build_details()


func prepare_turn(number: int) -> void:
	_finished = false
	turn_number = number
	drafting = true
	get_node("ZoneA").show()
	get_node("ZoneB").show()
	_shop_label.show()
	roll_shop()
	await turn_confirmed


func roll_shop() -> void:
	offers.clear()
	var candidates := pool.duplicate()
	# Sampling without replacement gives at most four distinct offers per turn.
	for i in range(mini(SHOP_LIMIT, candidates.size())):
		var index := _rng.randi_range(0, candidates.size() - 1)
		var offer: Dictionary = candidates.pop_at(index).duplicate()
		_serial += 1
		offer.token = _serial
		offers.append(offer)
	_refresh()


func accepts_token(token: int) -> bool:
	if not drafting or hand.size() >= HAND_LIMIT:
		return false
	for offer in offers:
		if offer.token == token:
			return true
	return false


func take_card(token: int) -> bool:
	if not accepts_token(token):
		return false
	for i in range(offers.size()):
		if offers[i].token == token:
			hand.append(offers.pop_at(i))
			_refresh()
			hand_changed.emit(hand.size())
			return true
	return false


func confirm_turn() -> void:
	if _finished:
		_finished = false
		next_round_requested.emit()
		return
	if not drafting:
		return
	if _deployment_mode and deployed.is_empty():
		_notice.text = "DEPLOY A UNIT FIRST"
		return
	drafting = false
	for entry in deployed.values():
		entry.unit.set_meta("has_battled", true)
	close_details()
	_refresh()
	get_node("ZoneA").hide()
	get_node("ZoneB").show()
	turn_confirmed.emit()


func finish_battle() -> void:
	drafting = false
	_finished = true
	_refresh()
	get_node("ZoneA").show()
	_shop_row.hide()
	_shop_label.hide()
	_next_button.disabled = false
	_next_button.get_node("Text").text = "NEXT ROUND"
	_notice.text = "BATTLE FINISHED"


func _refresh() -> void:
	_clear_row(_shop_row)
	_clear_row(_hand_row)
	for offer in offers:
		_shop_row.add_child(_card(offer, drafting, Vector2(72, 94), "shop"))
	for card in hand:
		_hand_row.add_child(_card(card, drafting, Vector2(64, 83), "hand"))
	_shop_label.text = "A / SHOP  %d/%d  |  TURN %d" % [offers.size(), SHOP_LIMIT, turn_number]
	_hand_label.text = "B / HAND  %d/%d  |  %s" % [hand.size(), HAND_LIMIT, "DRAG TO SHOP TO SELL" if drafting else "BATTLE / HAND LOCKED"]
	_notice.text = "FIELD %d/%d" % [deployed.size(), FIELD_LIMIT] if _deployment_mode else ("HAND FULL" if hand.size() == HAND_LIMIT else "CHOOSE CARDS")
	_next_button.disabled = not drafting or (_deployment_mode and deployed.is_empty())
	_next_button.get_node("Text").text = "BATTLE"
	_shop_row.visible = drafting


func _clear_row(row: HBoxContainer) -> void:
	for child in row.get_children():
		row.remove_child(child)
		child.queue_free()


func _card(data: Dictionary, draggable: bool, card_size: Vector2, source: String) -> TextureRect:
	var card := TextureRect.new()
	card.set_script(CARD_SCRIPT)
	card.shop = self
	card.token = data.token
	card.can_drag = draggable
	card.source = source
	card.texture = data.texture
	card.custom_minimum_size = card_size
	card.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	card.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
	card.mouse_filter = Control.MOUSE_FILTER_STOP
	card.tooltip_text = data.name
	return card


func accepts_hand_token(token: int) -> bool:
	if not drafting:
		return false
	for card in hand:
		if card.token == token:
			return true
	return false


func pick_unit(world_position: Vector2) -> Node2D:
	if not drafting:
		return null
	var picked: Node2D = null
	for entry in deployed.values():
		var unit: Node2D = entry.unit
		if Rect2(unit.position + Vector2(-16, -36), Vector2(32, 42)).has_point(world_position):
			if picked == null or unit.position.y > picked.position.y:
				picked = unit
	return picked


func _unit_token(unit: Node2D) -> int:
	for token in deployed:
		if deployed[token].unit == unit:
			return token
	return -1


func can_return_unit(unit: Node2D) -> bool:
	return drafting and hand.size() < HAND_LIMIT and is_instance_valid(unit) and _unit_token(unit) >= 0 and not unit.get_meta("has_battled", false)


func return_unit(unit: Node2D) -> bool:
	if not can_return_unit(unit):
		return false
	var token := _unit_token(unit)
	hand.append(deployed[token].card)
	deployed.erase(token)
	field.remove_child(unit)
	unit.queue_free()
	_refresh()
	hand_changed.emit(hand.size())
	return true


func can_place(data: Dictionary, world_position: Vector2) -> bool:
	if not drafting or not _deployment_mode:
		return false
	var moving_unit: Node2D = data.get("unit")
	if moving_unit != null:
		if not is_instance_valid(moving_unit) or _unit_token(moving_unit) < 0:
			return false
	elif data.get("source") != "hand" or not accepts_hand_token(data.get("token", -1)) or deployed.size() >= FIELD_LIMIT:
		return false
	var slot := deployment_slot(world_position)
	if slot < 0:
		return false
	var cell := DEPLOYMENT_CELLS[slot]
	for entry in deployed.values():
		if entry.unit != moving_unit and entry.unit.get_meta("grid_cell") == cell:
			return moving_unit != null
	return true


func deployment_slot(world_position: Vector2) -> int:
	var tiles: TileMapLayer = field.get_node("TileMapLayer")
	for i in range(DEPLOYMENT_CELLS.size()):
		var delta := tiles.to_local(world_position) - tiles.map_to_local(DEPLOYMENT_CELLS[i])
		if absf(delta.x) / 24.0 + absf(delta.y) / 12.0 <= 1.0:
			return i
	return -1


func place_card_or_unit(data: Dictionary, world_position: Vector2) -> bool:
	if not can_place(data, world_position):
		return false
	var tiles: TileMapLayer = field.get_node("TileMapLayer")
	var cell := DEPLOYMENT_CELLS[deployment_slot(world_position)]
	var position_on_field := tiles.to_global(tiles.map_to_local(cell))
	if data.has("unit"):
		var original_cell: Vector2i = data.unit.get_meta("grid_cell")
		for entry in deployed.values():
			if entry.unit != data.unit and entry.unit.get_meta("grid_cell") == cell:
				entry.unit.position = tiles.to_global(tiles.map_to_local(original_cell))
				entry.unit.set_meta("grid_cell", original_cell)
				break
		data.unit.position = position_on_field
		data.unit.set_meta("grid_cell", cell)
		return true
	for i in range(hand.size()):
		if hand[i].token != data.token:
			continue
		var card: Dictionary = hand.pop_at(i)
		var unit: Node2D = UNIT_SCENE.instantiate()
		unit.name = "Deployed_%d" % card.token
		unit.position = position_on_field
		unit.set_meta("team", "Ally")
		unit.set_meta("grid_cell", cell)
		unit.set_meta("card_token", card.token)
		unit.get_node("AnimatedSprite2D").material = _player_material
		field.add_child(unit)
		deployed[card.token] = {"unit": unit, "card": card}
		_refresh()
		hand_changed.emit(hand.size())
		return true
	return false


func reset_round_units() -> void:
	for unit in field.get_children():
		if unit.has_meta("team"):
			unit.reset_health()
			unit.get_node("AnimatedSprite2D").play("idle")


func sell_card(token: int) -> bool:
	if not accepts_hand_token(token):
		return false
	for i in range(hand.size()):
		if hand[i].token == token:
			hand.remove_at(i)
			close_details()
			_refresh()
			hand_changed.emit(hand.size())
			return true
	return false


func can_sell(data: Variant) -> bool:
	if not drafting or not data is Dictionary or data.get("card_shop") != self:
		return false
	if data.has("unit"):
		return is_instance_valid(data.unit) and _unit_token(data.unit) >= 0
	return data.get("source") == "hand" and accepts_hand_token(data.get("token", -1))


func sell_drop(data: Dictionary) -> void:
	if not can_sell(data):
		return
	if not data.has("unit"):
		sell_card(data.token)
		return
	var unit: Node2D = data.unit
	deployed.erase(_unit_token(unit))
	unit.hide()
	unit.queue_free()
	close_details()
	_refresh()


func _build_details() -> void:
	_details = NinePatchRect.new()
	_details.name = "Details"
	_details.texture = _atlas(Rect2(22, 137, 86, 71))
	_details.patch_margin_left = 8
	_details.patch_margin_right = 8
	_details.patch_margin_top = 8
	_details.patch_margin_bottom = 8
	_details.anchor_left = 1.0
	_details.anchor_right = 1.0
	_details.anchor_bottom = 1.0
	_details.offset_left = -300
	_details.offset_bottom = 0
	add_child(_details)
	# The panel frame has a transparent center; fill it with the sheet's white card interior.
	var paper := TextureRect.new()
	paper.texture = _atlas(Rect2(24, 484, 76, 100))
	paper.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	paper.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_details.add_child(paper)
	paper.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	paper.offset_left = 6
	paper.offset_top = 6
	paper.offset_right = -6
	paper.offset_bottom = -6
	_details_text = Label.new()
	_details_text.position = Vector2(22, 24)
	_details_text.size = Vector2(256, 430)
	_details_text.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	_details_text.modulate = Color.BLACK
	_details_text.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_details.add_child(_details_text)
	_details.hide()


func _show_details(title: String, team: String, hp: int = 100) -> void:
	if not _details.visible:
		var camera: Camera2D = field.get_node("Camera2D")
		_camera_position = camera.position
		camera.position.x += 150.0 / camera.zoom.x
		camera.force_update_scroll()
		get_node("ZoneA").offset_left = -335
		get_node("ZoneA").offset_right = 35
		get_node("ZoneB").offset_right = -300
	_details_text.text = "%s\n%s\n\nMOCK DETAILS\nHP  %d / 100\nAttack  30\nSpeed  10\n\nABILITY / Lorem Strike\nLorem ipsum dolor sit amet, consectetur adipiscing elit.\n\nPASSIVE / Lorem Guard\nSed do eiusmod tempor incididunt ut labore et dolore magna aliqua.\n\nPlaceholder abilities and stats." % [title, team, hp]
	_details.show()


func close_details() -> void:
	_inspected_unit = null
	if _details != null and _details.visible:
		field.get_node("Camera2D").position = _camera_position
		field.get_node("Camera2D").force_update_scroll()
		_details.hide()
		get_node("ZoneA").offset_left = -185
		get_node("ZoneA").offset_right = 185
		get_node("ZoneB").offset_right = 0


func show_card_details(token: int) -> void:
	_inspected_unit = null
	for card in offers + hand:
		if card.token == token:
			_show_details(card.name, "SHOP CARD" if card in offers else "PLAYER A / HAND")
			return


func pick_detail_unit(world_position: Vector2) -> Node2D:
	var picked: Node2D = null
	for unit in field.get_children():
		if not unit.has_meta("team") or unit.is_queued_for_deletion():
			continue
		if Rect2(unit.position + Vector2(-16, -36), Vector2(32, 42)).has_point(world_position):
			if picked == null or unit.position.y > picked.position.y:
				picked = unit
	return picked


func show_unit_details(unit: Node2D) -> void:
	if not is_instance_valid(unit):
		return
	var token := _unit_token(unit)
	_inspected_unit = unit
	var title: String = deployed[token].card.name if token >= 0 else "Orc"
	var team := "PLAYER A / FIELD" if token >= 0 else "PLAYER B / FIELD"
	_show_details(title, team + (" / DEAD" if unit.is_dead else " / ALIVE"), unit.health)

extends Control

var shop: Node
var _press_position: Vector2
var _drag_started := false

func _process(_delta: float) -> void:
	queue_redraw()

func _draw() -> void:
	if not shop.drafting or not get_viewport().gui_is_dragging():
		return
	var data: Variant = get_viewport().gui_get_drag_data()
	if not data is Dictionary or data.get("card_shop") != shop or (not data.has("unit") and data.get("source") != "hand"):
		return
	var tiles: TileMapLayer = shop.field.get_node("TileMapLayer")
	var transform_to_ui := get_global_transform_with_canvas().affine_inverse() * tiles.get_global_transform_with_canvas()
	var hovered: int = shop.deployment_slot(_world_position(get_local_mouse_position()))
	for i in range(shop.DEPLOYMENT_CELLS.size()):
		var center := tiles.map_to_local(shop.DEPLOYMENT_CELLS[i])
		var valid: bool = shop.can_place(data, tiles.to_global(center))
		var points := PackedVector2Array()
		for offset in [Vector2(0, -12), Vector2(24, 0), Vector2(0, 12), Vector2(-24, 0)]:
			points.append(transform_to_ui * (center + offset))
		var color := Color(1, 1, 1, 0.07 if valid else 0.03)
		if hovered == i:
			color = Color(1, 1, 1, 0.24) if valid else Color(1, 0.2, 0.2, 0.18)
		draw_colored_polygon(points, color)
		points.append(points[0])
		draw_polyline(points, Color(1, 1, 1, 0.4 if valid else 0.12), 1.0, false)

func _gui_input(event: InputEvent) -> void:
	if event is InputEventMouseButton and event.button_index == MOUSE_BUTTON_LEFT:
		if event.pressed:
			_press_position = event.position
			_drag_started = false
		elif not _drag_started and event.position.distance_to(_press_position) < 8:
			var unit: Node2D = shop.pick_detail_unit(_world_position(event.position))
			if unit != null:
				shop.show_unit_details(unit)
			else:
				shop.close_details()


func _world_position(position_in_ui: Vector2) -> Vector2:
	var viewport_position := get_global_transform_with_canvas() * position_in_ui
	return get_viewport().get_canvas_transform().affine_inverse() * viewport_position


func _get_drag_data(position_in_ui: Vector2) -> Variant:
	var unit: Node2D = shop.pick_unit(_world_position(position_in_ui))
	if unit == null:
		return null
	_drag_started = true
	var preview := Control.new()
	preview.position = Vector2(-32, -40)
	preview.size = Vector2(64, 80)
	var sprite := Sprite2D.new()
	var original: AnimatedSprite2D = unit.get_node("AnimatedSprite2D")
	sprite.texture = original.sprite_frames.get_frame_texture(original.animation, original.frame)
	sprite.position = Vector2(32, 40)
	sprite.scale = original.scale * shop.field.get_node("Camera2D").zoom
	sprite.flip_h = original.flip_h
	sprite.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
	preview.add_child(sprite)
	set_drag_preview(preview)
	return {"card_shop": shop, "unit": unit}


func _can_drop_data(position_in_ui: Vector2, data: Variant) -> bool:
	return data is Dictionary and data.get("card_shop") == shop and shop.can_place(data, _world_position(position_in_ui))


func _drop_data(position_in_ui: Vector2, data: Variant) -> void:
	shop.place_card_or_unit(data, _world_position(position_in_ui))

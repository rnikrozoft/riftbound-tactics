extends TextureRect

var shop: Node
var token: int = -1
var can_drag: bool = false
var source: String = "shop"
var _press_position: Vector2
var _drag_started := false

func _can_drop_data(_position: Vector2, data: Variant) -> bool:
	return source == "shop" and shop.can_sell(data)

func _drop_data(_position: Vector2, data: Variant) -> void:
	shop.sell_drop(data)

func _gui_input(event: InputEvent) -> void:
	if event is InputEventMouseButton and event.button_index == MOUSE_BUTTON_LEFT:
		accept_event()
		if event.pressed:
			_press_position = event.position
			_drag_started = false
		elif not _drag_started and event.position.distance_to(_press_position) < 8:
			shop.show_card_details(token)


func _get_drag_data(_position: Vector2) -> Variant:
	var valid: bool = shop.accepts_token(token) if source == "shop" else shop.accepts_hand_token(token)
	if not can_drag or not valid:
		return null
	_drag_started = true
	var preview := TextureRect.new()
	preview.texture = texture
	preview.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	preview.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
	preview.size = size
	preview.position = -size * 0.5
	preview.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
	set_drag_preview(preview)
	return {"card_shop": shop, "token": token, "source": source}

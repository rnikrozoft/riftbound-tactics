extends Control

var shop: Node

func _gui_input(event: InputEvent) -> void:
	if event is InputEventMouseButton and event.button_index == MOUSE_BUTTON_LEFT and not event.pressed and not get_viewport().gui_is_dragging():
		shop.close_details()


func _can_drop_data(_position: Vector2, data: Variant) -> bool:
	if not data is Dictionary or data.get("card_shop") != shop:
		return false
	if data.has("unit"):
		return shop.can_return_unit(data.unit)
	return data.get("source", "shop") == "shop" and shop.accepts_token(data.get("token", -1))


func _drop_data(_position: Vector2, data: Variant) -> void:
	if data.has("unit"):
		shop.return_unit(data.unit)
	else:
		shop.take_card(data.token)

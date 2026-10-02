extends Control

var shop: Node

func _can_drop_data(_position: Vector2, data: Variant) -> bool:
	return shop.can_sell(data)

func _drop_data(_position: Vector2, data: Variant) -> void:
	shop.sell_drop(data)

func _gui_input(event: InputEvent) -> void:
	if event is InputEventMouseButton and event.button_index == MOUSE_BUTTON_LEFT and not event.pressed and not get_viewport().gui_is_dragging():
		shop.close_details()

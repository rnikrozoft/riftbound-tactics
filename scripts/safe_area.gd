extends MarginContainer
## Place HUD and touch controls inside this container's Content node.
## Account for letterboxing as well as device cutouts.

@export_range(0, 64, 1) var edge_padding: int = 16

var _refresh_elapsed: float = 0.0


func _ready() -> void:
	resized.connect(_update_safe_area)
	_update_safe_area.call_deferred()


func _process(delta: float) -> void:
	# Insets can change after rotation or when the system UI changes.
	_refresh_elapsed += delta
	if _refresh_elapsed >= 0.25:
		_refresh_elapsed = 0.0
		_update_safe_area()


func _update_safe_area() -> void:
	var insets := Vector4.ZERO
	if OS.has_feature("android") or OS.has_feature("ios"):
		var display_size := Vector2(DisplayServer.window_get_size())
		var safe_rect := Rect2(DisplayServer.get_display_safe_area())
		var display_rect := Rect2(Vector2.ZERO, display_size)
		safe_rect = safe_rect.intersection(display_rect)
		if display_size.x > 0.0 and display_size.y > 0.0 and safe_rect.has_area():
			# Include the stretch scale and letterbox offset when mapping to UI.
			var local_to_screen := get_viewport().get_screen_transform() * get_global_transform_with_canvas()
			var local_safe := local_to_screen.affine_inverse() * safe_rect
			local_safe = local_safe.intersection(Rect2(Vector2.ZERO, size))
			insets = Vector4(
				maxf(0.0, local_safe.position.x),
				maxf(0.0, local_safe.position.y),
				maxf(0.0, size.x - local_safe.end.x),
				maxf(0.0, size.y - local_safe.end.y)
			)
	_set_margin("margin_left", insets.x)
	_set_margin("margin_top", insets.y)
	_set_margin("margin_right", insets.z)
	_set_margin("margin_bottom", insets.w)


func _set_margin(margin_name: StringName, inset: float) -> void:
	var margin := ceili(inset) + edge_padding
	if get_theme_constant(margin_name) != margin:
		add_theme_constant_override(margin_name, margin)

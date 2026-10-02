extends Node2D


func _ready() -> void:
	get_node("UI/SafeArea/Content/EffectsLabButton").pressed.connect(_open_effects_lab)
	if OS.get_name() in ["Windows", "Linux", "macOS"]:
		var window := get_window()
		var desktop_size := Vector2i(1280, 720)
		window.mode = Window.MODE_WINDOWED
		window.unresizable = true
		window.maximize_disabled = true
		window.min_size = desktop_size
		window.max_size = desktop_size
		window.size = desktop_size


func _open_effects_lab() -> void:
	get_tree().change_scene_to_file("res://scenes/damage_simulator.tscn")

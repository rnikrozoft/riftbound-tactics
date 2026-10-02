extends CharacterBody2D

signal health_changed(current: int, maximum: int)
signal died

@export_range(1, 1000, 1) var max_health: int = 100
var health: int = 0
var is_dead: bool = false
const BAR_FRAME = preload("res://assets/Complete_UI_Essential_Pack_Free/01_Flat_Theme/Sprites/UI_Flat_Bar05a.png")
const PLAYER_A_FILL = preload("res://assets/Complete_UI_Essential_Pack_Free/01_Flat_Theme/Sprites/UI_Flat_BarFill01f.png")
const PLAYER_B_FILL = preload("res://assets/Complete_UI_Essential_Pack_Free/01_Flat_Theme/Sprites/UI_Flat_BarFill01c.png")

var _health_bar: TextureProgressBar


func _ready() -> void:
	health = max_health
	_health_bar = TextureProgressBar.new()
	_health_bar.name = "HealthBar"
	_health_bar.position = Vector2(-16, -43)
	_health_bar.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_health_bar.max_value = max_health
	_health_bar.value = health
	_health_bar.texture_under = BAR_FRAME
	var fill := AtlasTexture.new()
	fill.atlas = PLAYER_A_FILL if get_meta("team", "Ally") == "Ally" else PLAYER_B_FILL
	# Use the pack's original pixels, cropped to fit inside its bar frame.
	fill.region = Rect2(0, 0, 28, 3)
	_health_bar.texture_progress = fill
	_health_bar.texture_progress_offset = Vector2(2, 3)
	add_child(_health_bar)
	_health_bar.size = Vector2(32, 10)


func take_damage(amount: int) -> void:
	if is_dead:
		return
	health = clampi(health - maxi(0, amount), 0, max_health)
	_health_bar.value = health
	health_changed.emit(health, max_health)
	if health == 0:
		is_dead = true
		_health_bar.hide()
		died.emit()


func reset_health() -> void:
	health = max_health
	is_dead = false
	_health_bar.value = health
	_health_bar.show()
	health_changed.emit(health, max_health)

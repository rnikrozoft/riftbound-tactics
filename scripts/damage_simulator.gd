extends Node

const BUTTON_NORMAL = preload("res://assets/Complete_UI_Essential_Pack_Free/01_Flat_Theme/Sprites/UI_Flat_Button01a_1.png")
const BUTTON_PRESSED = preload("res://assets/Complete_UI_Essential_Pack_Free/01_Flat_Theme/Sprites/UI_Flat_Button01a_2.png")
const BUTTON_HOVER = preload("res://assets/Complete_UI_Essential_Pack_Free/01_Flat_Theme/Sprites/UI_Flat_Button01a_3.png")
const CHOICES_PATH = "user://effect_choices.json"

var catalog: Array = []
var selected_index: int = -1
var favorites: Array = []
var _categories: Array[String] = ["Impacts"]
var _category_index: int = 0
var _cache: Dictionary = {}
var _list: VBoxContainer
var _title: Label
var _status: Label
var _category_button: TextureButton
var _keep_button: TextureButton
var _target: Node2D
var _sprite: AnimatedSprite2D
var _effect: AnimatedSprite2D
var _camera: Camera2D
var _busy: bool = false
var _loop: bool = false
var _hitstop: bool = true
var _shake: bool = true
var _shake_left: float = 0.0
var _loop_elapsed: float = 0.0


func _ready() -> void:
	catalog = JSON.parse_string(FileAccess.get_file_as_string("res://data/effect_catalog.json"))
	for entry in catalog:
		if not _categories.has(entry.category):
			_categories.append(entry.category)
	if FileAccess.file_exists(CHOICES_PATH):
		var saved = JSON.parse_string(FileAccess.get_file_as_string(CHOICES_PATH))
		if saved is Array:
			favorites = saved
	var field := $Battlefield
	field.get_node("BattleDemo").set_process(false)
	field.get_node("UI/SafeArea/Content/EffectsLabButton").hide()
	field.get_node("UI/SafeArea/Content/CardShop").hide()
	for unit in field.get_children():
		if unit.has_meta("team"):
			unit.visible = unit.name == &"Enemy_02"
	_target = field.get_node("Enemy_02")
	_target.position = Vector2(480, 408)
	_sprite = _target.get_node("AnimatedSprite2D")
	_camera = field.get_node("Camera2D")
	_effect = AnimatedSprite2D.new()
	_effect.name = "DamageEffect"
	_effect.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
	_effect.z_index = 10
	_effect.position = Vector2(0, -18)
	_target.add_child(_effect)
	_build_ui()
	_rebuild_list()


func _process(delta: float) -> void:
	_shake_left = maxf(0.0, _shake_left - delta)
	if _shake_left > 0.0:
		_camera.offset = Vector2(randf_range(-2.5, 2.5), randf_range(-2.5, 2.5)) * _shake_left / 0.2
	else:
		_camera.offset = Vector2.ZERO
	if _loop:
		_loop_elapsed += delta
		if _loop_elapsed >= 1.4 and not _busy:
			_loop_elapsed = 0.0
			play_damage()


func _button(text: String, action: Callable, width: float = 340.0) -> TextureButton:
	var button := TextureButton.new()
	button.texture_normal = BUTTON_NORMAL
	button.texture_pressed = BUTTON_PRESSED
	button.texture_hover = BUTTON_HOVER
	button.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
	button.ignore_texture_size = true
	button.stretch_mode = TextureButton.STRETCH_SCALE
	button.custom_minimum_size = Vector2(width, 36)
	button.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	var label := Label.new()
	label.name = "Text"
	label.text = text
	label.modulate = Color.BLACK
	label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	label.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	button.add_child(label)
	button.pressed.connect(action)
	return button


func _build_ui() -> void:
	var content := $LabUI/SafeArea/Content
	var sidebar := VBoxContainer.new()
	sidebar.set_anchors_and_offsets_preset(Control.PRESET_LEFT_WIDE)
	sidebar.offset_right = 360
	content.add_child(sidebar)
	var heading := Label.new()
	heading.text = "DAMAGE EFFECTS / %d VARIANTS" % catalog.size()
	sidebar.add_child(heading)
	_category_button = _button("CATEGORY: Impacts", _next_category)
	sidebar.add_child(_category_button)
	var scroll := ScrollContainer.new()
	scroll.size_flags_vertical = Control.SIZE_EXPAND_FILL
	scroll.horizontal_scroll_mode = ScrollContainer.SCROLL_MODE_DISABLED
	sidebar.add_child(scroll)
	_list = VBoxContainer.new()
	_list.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	scroll.add_child(_list)
	var controls := VBoxContainer.new()
	controls.anchor_left = 1.0
	controls.anchor_right = 1.0
	controls.offset_left = -370
	controls.offset_bottom = 360
	content.add_child(controls)
	_title = Label.new()
	_title.custom_minimum_size = Vector2(370, 50)
	_title.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	controls.add_child(_title)
	controls.add_child(_button("PLAY DAMAGE (-25 HP)", play_damage))
	controls.add_child(_button("LOOP: OFF", func(): _loop = not _loop; _set_toggle(controls, 2, "LOOP", _loop)))
	controls.add_child(_button("HITSTOP: ON", func(): _hitstop = not _hitstop; _set_toggle(controls, 3, "HITSTOP", _hitstop)))
	controls.add_child(_button("CAMERA SHAKE: ON", func(): _shake = not _shake; _set_toggle(controls, 4, "CAMERA SHAKE", _shake)))
	controls.add_child(_button("RESET HP", _reset_target))
	_keep_button = _button("KEEP THIS EFFECT", _toggle_favorite)
	controls.add_child(_keep_button)
	_status = Label.new()
	_status.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	controls.add_child(_status)
	controls.add_child(_button("BACK TO BATTLE", func(): get_tree().change_scene_to_file("res://scenes/main.tscn")))


func _set_toggle(container: VBoxContainer, index: int, title: String, enabled: bool) -> void:
	container.get_child(index).get_node("Text").text = "%s: %s" % [title, "ON" if enabled else "OFF"]


func _next_category() -> void:
	if _busy:
		return
	_category_index = (_category_index + 1) % _categories.size()
	_category_button.get_node("Text").text = "CATEGORY: " + _categories[_category_index]
	_rebuild_list()


func _rebuild_list() -> void:
	for child in _list.get_children():
		_list.remove_child(child)
		child.queue_free()
	var first := -1
	for i in range(catalog.size()):
		if catalog[i].category != _categories[_category_index]:
			continue
		if first == -1:
			first = i
		var caption: String = catalog[i].name.replace("_", " ")
		var button := _button(caption, _select_and_play.bind(i))
		button.get_node("Text").add_theme_font_size_override("font_size", 12)
		button.tooltip_text = catalog[i].name
		_list.add_child(button)
	if first >= 0:
		_select(first)


func _select_and_play(index: int) -> void:
	if _busy:
		return
	_select(index)
	play_damage()


func _select(index: int) -> void:
	selected_index = index
	_title.text = catalog[index].name + "\n15 FPS / " + catalog[index].category
	_keep_button.get_node("Text").text = "REMOVE FROM PICKS" if favorites.has(catalog[index].texture) else "KEEP THIS EFFECT"
	_status.text = "%d effects kept" % favorites.size()


func play_damage() -> void:
	if _busy or selected_index < 0:
		return
	_busy = true
	var entry: Dictionary = catalog[selected_index]
	if not _cache.has(selected_index):
		var frames := SpriteFrames.new()
		frames.add_animation("effect")
		frames.set_animation_loop("effect", false)
		frames.set_animation_speed("effect", 15.0)
		var texture: Texture2D = load(entry.texture)
		for rect in entry.frames:
			var frame := AtlasTexture.new()
			frame.atlas = texture
			frame.region = Rect2(rect[0], rect[1], rect[2], rect[3])
			frames.add_frame("effect", frame)
		_cache[selected_index] = frames
	if _target.is_dead:
		_target.reset_health()
		_sprite.play("idle")
	_target.take_damage(25)
	_sprite.play("die" if _target.is_dead else "hit")
	_sprite.set_frame_and_progress(0, 0.0)
	_effect.sprite_frames = _cache[selected_index]
	_effect.show()
	_effect.play("effect")
	_effect.set_frame_and_progress(0, 0.0)
	if _shake:
		_shake_left = 0.2
	if _hitstop:
		_sprite.speed_scale = 0.0
		_effect.speed_scale = 0.0
		await get_tree().create_timer(0.075, true, false, true).timeout
		_sprite.speed_scale = 1.0
		_effect.speed_scale = 1.0
	while _effect.is_playing() or _sprite.is_playing():
		await get_tree().process_frame
	_effect.hide()
	if not _target.is_dead:
		_sprite.play("idle")
	_status.text = "HP %d / %d | %d effects kept" % [_target.health, _target.max_health, favorites.size()]
	_busy = false


func _reset_target() -> void:
	if _busy:
		return
	_target.reset_health()
	_sprite.play("idle")


func _toggle_favorite() -> void:
	if selected_index < 0:
		return
	var path: String = catalog[selected_index].texture
	if favorites.has(path):
		favorites.erase(path)
	else:
		favorites.append(path)
	var file := FileAccess.open(CHOICES_PATH, FileAccess.WRITE)
	if file:
		file.store_string(JSON.stringify(favorites, "\t"))
	_select(selected_index)

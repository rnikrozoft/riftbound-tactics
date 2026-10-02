extends Node
## Alternating team combat with health, death, and a winner.

signal turn_started(team: String, attacker: Node2D, target: Node2D)
signal impact(attacker: Node2D, target: Node2D)
signal turn_finished(team: String, attacker: Node2D)
signal battle_finished(winner: String)

@export var start_delay: float = 0.8
@export var auto_start: bool = true
@export var card_shop_enabled: bool = false
@export var turn_delay: float = 0.45
@export var dash_duration: float = 0.24
@export var return_duration: float = 0.32
@export var hitstop_duration: float = 0.075
@export var shake_duration: float = 0.2
@export var shake_strength: float = 2.5
@export var arena_float_amplitude: float = 1.5
@export var arena_float_period: float = 6.0
@export_range(1, 1000, 1) var damage_min: int = 25
@export_range(1, 1000, 1) var damage_max: int = 40

var _team_a: Array[Node2D] = []
var _team_b: Array[Node2D] = []
var _sprites: Array[AnimatedSprite2D] = []
var _rng := RandomNumberGenerator.new()
var _shake_rng := RandomNumberGenerator.new()
var _camera: Camera2D
var _camera_offset := Vector2.ZERO
var _shake_remaining: float = 0.0
var _frozen_speeds: Array[float] = []
var _float_time: float = 0.0


func _ready() -> void:
	_rng.randomize()
	_shake_rng.randomize()
	_camera = get_parent().get_node("Camera2D")
	_camera_offset = _camera.offset
	_collect_teams()
	if auto_start:
		_run_battle.call_deferred()


func _collect_teams() -> void:
	_team_a.clear()
	_team_b.clear()
	_sprites.clear()
	for child in get_parent().get_children():
		if child is Node2D and child.has_meta("team") and not child.is_queued_for_deletion():
			if child.get_meta("team") == "Ally":
				_team_a.append(child)
			else:
				_team_b.append(child)
			_sprites.append(child.get_node("AnimatedSprite2D"))


func _process(delta: float) -> void:
	_float_time += delta
	var floating_offset := Vector2(0, sin(_float_time * TAU / maxf(arena_float_period, 0.1)) * arena_float_amplitude)
	if _shake_remaining > 0.0:
		_shake_remaining = maxf(0.0, _shake_remaining - delta)
		var strength := shake_strength * _shake_remaining / maxf(shake_duration, 0.001)
		_camera.offset = _camera_offset + floating_offset + Vector2(
			_shake_rng.randf_range(-strength, strength),
			_shake_rng.randf_range(-strength, strength)
		)
	else:
		_camera.offset = _camera_offset + floating_offset


func _run_battle() -> void:
	await get_tree().create_timer(start_delay).timeout
	if card_shop_enabled:
		var shop := get_parent().get_node("UI/SafeArea/Content/CardShop")
		var round_number := 1
		while is_inside_tree():
			get_parent().get_node("UI/SafeArea/Content/BattleResult").hide()
			await shop.prepare_turn(round_number)
			_collect_teams()
			await _run_combat()
			await shop.next_round_requested
			shop.reset_round_units()
			round_number += 1
	else:
		await _run_combat()


func _run_combat() -> void:
	var a_turn := true
	while is_inside_tree() and not _team_a.is_empty() and not _team_b.is_empty():
		var attackers := _team_a if a_turn else _team_b
		var targets := _team_b if a_turn else _team_a
		var attacker := attackers[_rng.randi_range(0, attackers.size() - 1)]
		var target := targets[_rng.randi_range(0, targets.size() - 1)]
		var team := "A" if a_turn else "B"
		turn_started.emit(team, attacker, target)
		await _take_turn(attacker, target)
		turn_finished.emit(team, attacker)
		a_turn = not a_turn
		if _team_a.is_empty() or _team_b.is_empty():
			break
		await get_tree().create_timer(turn_delay).timeout
	var winner := "A" if not _team_a.is_empty() else "B"
	var result: Control = get_parent().get_node("UI/SafeArea/Content/BattleResult")
	result.get_node("Text").text = "PLAYER %s WINS" % winner
	result.show()
	if card_shop_enabled:
		get_parent().get_node("UI/SafeArea/Content/CardShop").finish_battle()
	battle_finished.emit(winner)


func _take_turn(attacker: Node2D, target: Node2D) -> void:
	var sprite: AnimatedSprite2D = attacker.get_node("AnimatedSprite2D")
	var target_sprite: AnimatedSprite2D = target.get_node("AnimatedSprite2D")
	var home := attacker.position
	var target_home := target.position
	var original_facing := sprite.flip_h
	var target_facing := target_sprite.flip_h
	var direction := (target_home - home).normalized()
	var attack_position := target_home - direction * 22.0
	sprite.flip_h = target_home.x < home.x
	sprite.play("walk")
	var dash := create_tween().set_trans(Tween.TRANS_QUAD).set_ease(Tween.EASE_IN)
	dash.tween_property(attacker, "position", attack_position, dash_duration)
	await dash.finished
	sprite.play("attack")
	sprite.set_frame_and_progress(0, 0.0)
	# The weapon first extends on frame 2 in the supplied attack animations.
	while sprite.frame < 2 and sprite.is_playing():
		await sprite.frame_changed
	target_sprite.flip_h = attack_position.x < target_home.x
	target.take_damage(_rng.randi_range(mini(damage_min, damage_max), maxi(damage_min, damage_max)))
	var lethal: bool = target.is_dead
	if lethal:
		_team_a.erase(target)
		_team_b.erase(target)
		target_sprite.play("die")
	else:
		target_sprite.play("hit")
	_shake_remaining = shake_duration
	_freeze_sprites()
	impact.emit(attacker, target)
	await get_tree().create_timer(hitstop_duration, true, false, true).timeout
	_restore_sprites()
	if not lethal:
		var recoil := create_tween()
		recoil.tween_property(target, "position", target_home + direction * 3.0, 0.08)
		recoil.tween_property(target, "position", target_home, 0.16)
		await recoil.finished
	# Polling also handles an attack that finished during the recoil tween.
	while sprite.is_playing():
		await get_tree().process_frame
	sprite.play("walk")
	var retreat := create_tween().set_trans(Tween.TRANS_QUAD).set_ease(Tween.EASE_OUT)
	retreat.tween_property(attacker, "position", home, return_duration)
	await retreat.finished
	attacker.position = home
	target.position = target_home
	sprite.flip_h = original_facing
	target_sprite.flip_h = target_facing
	sprite.play("idle")
	if lethal:
		while target_sprite.is_playing():
			await get_tree().process_frame
	else:
		target_sprite.play("idle")


func _freeze_sprites() -> void:
	_frozen_speeds.clear()
	for sprite in _sprites:
		_frozen_speeds.append(sprite.speed_scale)
		sprite.speed_scale = 0.0


func _restore_sprites() -> void:
	for i in range(_frozen_speeds.size()):
		if is_instance_valid(_sprites[i]):
			_sprites[i].speed_scale = _frozen_speeds[i]
	_frozen_speeds.clear()


func _exit_tree() -> void:
	_restore_sprites()
	if is_instance_valid(_camera):
		_camera.offset = _camera_offset

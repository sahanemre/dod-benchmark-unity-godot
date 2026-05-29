class_name EntitySpawner extends RefCounted

## EntityMover node'larini olusturur ve yok eder.
## Tek sorumluluk: entity yasam dongusu yonetimi.

var _entities: Array[EntityMover] = []
var _parent: Node
var _move_speed: float
var _screen_min: Vector2
var _screen_max: Vector2


func _init(parent: Node, move_speed: float, screen_min: Vector2, screen_max: Vector2) -> void:
	_parent = parent
	_move_speed = move_speed
	_screen_min = screen_min
	_screen_max = screen_max


func get_active_count() -> int:
	return _entities.size()


func spawn(count: int) -> void:
	clear()
	for i in range(count):
		var entity := EntityMover.new()
		_parent.add_child(entity)
		entity.initialize(_move_speed, _screen_min, _screen_max)
		_entities.append(entity)


func clear() -> void:
	for entity in _entities:
		if is_instance_valid(entity):
			entity.queue_free()
	_entities.clear()

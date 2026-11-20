class_name EntityMover extends Node2D

## OOP entity: kendi konumunu, hizini ve rengini icerir;
## kendi hareketini ve ciziminini yonetir.
## Tek sorumluluk: entity verisi + davranisi.

var velocity: Vector2 = Vector2.ZERO
var screen_min: Vector2 = Vector2.ZERO
var screen_max: Vector2 = Vector2.ZERO
var _color: Color = Color.WHITE

## Cizim boyutu (mantiksal px), tum entity'ler icin ortak. Yalnizca gorunum;
## hareket/sekme mantigi entity'yi nokta olarak ele alir. published: 8 (makale),
## parity: 6,75 (ekranda Unity ile ayni boyut). Yonetici spawn'dan once ayarlar.
static var draw_size: float = 8.0


func initialize(speed: float, s_min: Vector2, s_max: Vector2) -> void:
	screen_min = s_min
	screen_max = s_max
	var angle := randf() * TAU
	velocity = Vector2(cos(angle), sin(angle)) * speed
	position = Vector2(
		randf_range(s_min.x, s_max.x),
		randf_range(s_min.y, s_max.y)
	)
	_color = Color(randf_range(0.3, 1.0), randf_range(0.3, 1.0), randf_range(0.3, 1.0))
	queue_redraw()


func _process(delta: float) -> void:
	position += velocity * delta
	if position.x < screen_min.x or position.x > screen_max.x:
		velocity.x = -velocity.x
		position.x = clamp(position.x, screen_min.x, screen_max.x)
	if position.y < screen_min.y or position.y > screen_max.y:
		velocity.y = -velocity.y
		position.y = clamp(position.y, screen_min.y, screen_max.y)


func _draw() -> void:
	var half := draw_size * 0.5
	draw_rect(Rect2(-half, -half, draw_size, draw_size), _color)

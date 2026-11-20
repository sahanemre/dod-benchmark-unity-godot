class_name EntityS2 extends Node2D

## S2 OOP entity'si: kendi pozisyonunu ve hizini KENDI nesnesinde (script
## alanlarinda) tutar; hareketini kendi _process'inde yapar (S1 ile ayni
## mantik). Node2D.position yalnizca cizim icin, karede bir kez guncellenir.
## Carpisma testi (S2ManagerOOP) bu alanlari nesne referansi uzerinden okur/yazar.

var pos: Vector2 = Vector2.ZERO   ## float32 (Vector2), C#/C++ ile ayni hassasiyet
var vel: Vector2 = Vector2.ZERO
var index: int = 0                ## spawn sirasi (cift sirasi j > i icin)
var cell: int = 0                 ## bu karedeki grid hucresi
var steps: int = 0                ## yapilan hareket adimi (kare hizalamasi icin)
var screen_min: Vector2 = Vector2.ZERO
var screen_max: Vector2 = Vector2.ZERO
var _color: Color = Color.WHITE

## Dogrulama modunda sabit dt (> 0 ise delta yerine kullanilir)
static var fixed_dt: float = 0.0
## Cizim boyutu (mantiksal px) = 2r
static var draw_size: float = 6.75


func setup(p_index: int, p_pos: Vector2, p_vel: Vector2, p_color: Color,
		s_min: Vector2, s_max: Vector2) -> void:
	index = p_index
	pos = p_pos
	vel = p_vel
	_color = p_color
	screen_min = s_min
	screen_max = s_max
	position = pos
	queue_redraw()


func _process(delta: float) -> void:
	var dt := fixed_dt if fixed_dt > 0.0 else delta
	# S1 ile ayni hareket ve sekme (alanlar uzerinde)
	pos += vel * dt
	if pos.x < screen_min.x or pos.x > screen_max.x:
		vel.x = -vel.x
		pos.x = clamp(pos.x, screen_min.x, screen_max.x)
	if pos.y < screen_min.y or pos.y > screen_max.y:
		vel.y = -vel.y
		pos.y = clamp(pos.y, screen_min.y, screen_max.y)
	position = pos
	steps += 1


func _draw() -> void:
	var half := draw_size * 0.5
	draw_rect(Rect2(-half, -half, draw_size, draw_size), _color)

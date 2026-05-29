class_name EntityRenderer extends Node2D

## Tum entity'leri tek Node2D icerisinde, tek _draw() gecisinde cizer.
## Tek sorumluluk: gorsel cikti. Veri guncelleme yapmaz.
##
## OOP farki: N adet Node2D._draw() cagrisi yerine tek bir dongu.
## draw_rect() cagrisi sayisi ayni olsa da sahne agaci traversal overhead'i sifirdir.

const HALF_SIZE := Vector2(4.0, 4.0)
const RECT_SIZE := Vector2(8.0, 8.0)

var _data: EntityData = null


func set_data(data: EntityData) -> void:
	_data = data


func _process(_delta: float) -> void:
	if _data != null and _data.count > 0:
		queue_redraw()


func _draw() -> void:
	if _data == null:
		return
	for i in range(_data.count):
		draw_rect(Rect2(_data.positions[i] - HALF_SIZE, RECT_SIZE), _data.colors[i])

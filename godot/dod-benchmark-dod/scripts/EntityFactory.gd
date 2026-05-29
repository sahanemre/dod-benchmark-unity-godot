class_name EntityFactory extends RefCounted

## EntityData dizilerini baslatir ve temizler.
## Tek sorumluluk: veri yasam dongusu yonetimi (spawn / clear).
## Node olusturmaz — sadece dizi elemanlari doldurur.

static func spawn(data: EntityData, count: int, speed: float,
		screen_min: Vector2, screen_max: Vector2) -> void:
	data.resize(count)
	for i in range(count):
		var angle := randf() * TAU
		data.positions[i] = Vector2(
			randf_range(screen_min.x, screen_max.x),
			randf_range(screen_min.y, screen_max.y)
		)
		data.velocities[i] = Vector2(cos(angle), sin(angle)) * speed
		data.colors[i] = Color(
			randf_range(0.3, 1.0),
			randf_range(0.3, 1.0),
			randf_range(0.3, 1.0)
		)


static func clear(data: EntityData) -> void:
	data.clear()

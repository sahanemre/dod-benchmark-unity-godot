class_name MovementProcessor extends RefCounted

## Hareket sistemi: EntityData dizileri uzerinde toplu (batch) guncelleme yapar.
## Tek sorumluluk: pozisyon + sekme mantigi. Durum tutmaz, Node degildir.
##
## DOD prensibinin ozudur: nesne basina _process() cagrisi yok,
## tek bir sikistirma dongusuyle tum veri ardisik islenir.

static func process(data: EntityData, delta: float,
		screen_min: Vector2, screen_max: Vector2) -> void:
	for i in range(data.count):
		var pos: Vector2 = data.positions[i]
		var vel: Vector2 = data.velocities[i]

		pos += vel * delta

		if pos.x < screen_min.x or pos.x > screen_max.x:
			vel.x = -vel.x
			pos.x = clamp(pos.x, screen_min.x, screen_max.x)
		if pos.y < screen_min.y or pos.y > screen_max.y:
			vel.y = -vel.y
			pos.y = clamp(pos.y, screen_min.y, screen_max.y)

		data.positions[i] = pos
		data.velocities[i] = vel

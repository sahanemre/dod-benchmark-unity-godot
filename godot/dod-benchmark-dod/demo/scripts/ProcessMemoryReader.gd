class_name ProcessMemoryReader extends RefCounted

## Isletim sistemi duzeyinde bellek olcumu (motor profiler'i DEGIL).
## Tek sorumluluk: surecin "Private Bytes" degerini okumak.
##
## GDScript OS API'lerine erisemedigi icin ortak "procmem" GDExtension'ini
## kullanir (godot/procmem). Eklenti yuklenmemisse is_available() false doner;
## manuel modda bu sorun degildir, gozetimsiz modda kosu hata koduyla biter.

var _native: RefCounted = null


func _init() -> void:
	if ClassDB.class_exists("ProcessMemory"):
		_native = ClassDB.instantiate("ProcessMemory")


func is_available() -> bool:
	return _native != null


## Private bytes; okunamazsa -1.
func get_private_bytes() -> int:
	if _native == null:
		return -1
	return _native.get_private_bytes()

class_name EntityData extends RefCounted

## DOD veri blogu: tum entity verileri ayri, bitisik dizilerde tutulur (SoA).
## Tek sorumluluk: saf veri deposu — hic bir mantik icermez.
##
## SoA (Structure of Arrays) duzeni, AoS (Array of Structures / OOP nesneleri)
## yerine tercih edilir; islemci onbellegini pozisyon guncelleme dongusunde
## daha verimli kullanir: positions dizisi ardisik okunurken velocities dizisi
## de ardisik okunur, her entity nesnesi icin atlama yapmak gerekmez.

var count: int = 0
var positions: PackedVector2Array
var velocities: PackedVector2Array
var colors: PackedColorArray


func resize(n: int) -> void:
	count = n
	positions.resize(n)
	velocities.resize(n)
	colors.resize(n)


func clear() -> void:
	count = 0
	positions.resize(0)
	velocities.resize(0)
	colors.resize(0)

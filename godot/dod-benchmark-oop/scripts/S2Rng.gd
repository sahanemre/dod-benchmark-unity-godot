class_name S2Rng extends RefCounted

## S2 ortak RNG: murmur3 fmix32 ile karistirilmis seed + xorshift32.
## C++ (MovementWorld), C# (S2Rng.cs) ve referans (s2_reference.c) ile birebir
## ayni sayilari uretir. GDScript int'i 64 bit isaretli oldugu icin 32 bit
## aritmetik maskeyle yapilir; carpma tasma olmadan 16 bitlik parcalarla.
##
## float32 uyumu: next_float 24 bit oldugu icin double ve float'ta aynidir.
## range() sonucunu float32'de hesaplamak icin Vector2 (float32) islemleri kullanilir.

const MASK := 0xFFFFFFFF

var _state: int = 0


func _init(seed_value: int) -> void:
	var h := seed_value & MASK
	h ^= h >> 16
	h = _mul32(h, 0x85ebca6b)
	h ^= h >> 13
	h = _mul32(h, 0xc2b2ae35)
	h ^= h >> 16
	_state = h if h != 0 else 0x9e3779b9


## 32 bit carpma (mod 2^32), 64 bit isaretli int'te tasmadan
static func _mul32(a: int, b: int) -> int:
	var lo := (a * (b & 0xFFFF)) & MASK
	var hi := ((a * (b >> 16)) & 0xFFFF) << 16
	return (lo + hi) & MASK


## [0, 1) (24 bit)
func next_float() -> float:
	_state = (_state ^ (_state << 13)) & MASK
	_state = _state ^ (_state >> 17)
	_state = (_state ^ (_state << 5)) & MASK
	return float(_state & 0x00FFFFFF) / 16777216.0


## Tek cekilis: lo + (hi - lo) * u, float32'de (C# / C++ ile ayni yuvarlama)
func range1(lo: float, hi: float) -> float:
	var u := next_float()
	return (Vector2(lo, 0.0) + (Vector2(hi, 0.0) - Vector2(lo, 0.0)) * u).x


## Iki cekilis (once x, sonra y): lo + (hi - lo) * u, float32'de
func range2(lo: Vector2, hi: Vector2) -> Vector2:
	var u := Vector2(next_float(), next_float())
	return lo + (hi - lo) * u

class_name S2Params extends RefCounted

## S2 (carpisma) ortak parametreleri. Dort konfigurasyonda AYNI sayilar
## (Unity'de S2Params.cs, referans: tools/s2_reference/s2_reference.c).
##
## Ortak simulasyon uzayi = Godot'un mantiksal 1280x720 dunyasi. Unity'de S2
## kamerasi ayni uzayi gosterir (orthographic size 360, merkez 640,360).

const WORLD_MIN := Vector2(10.0, 10.0)
const WORLD_MAX := Vector2(1270.0, 710.0)
const RADIUS := 3.375          ## S1 parity entity'si 6,75 -> r = 3,375
const CELL := 6.75             ## hucre = 2r
const D2_MAX := 45.5625        ## (2r)^2, ikilik tabanda tam
const SPEED := 300.0
const TWO_PI := 6.28318530718
const FIXED_DT := 1.0 / 60.0   ## dogrulama modu
## Dogrulama kosusunda O(N^2) kontrolun yapildigi en buyuk N
const VALIDATE_BRUTEFORCE_MAX_N := 2000

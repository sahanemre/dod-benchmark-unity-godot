class_name RunArgs extends RefCounted

## Gozetimsiz (unattended) kosu argumanlari.
## Tek sorumluluk: komut satirini okumak ve dogrulamak.
##
## Godot kullanici argumanlari "--" isaretinden SONRA verilir:
##   oyun.exe -- --n=50000 --run-index=3 --out=C:/sonuc/x.csv
## ("--n 50000" bicimi de kabul edilir.)
##
## --n verilmezse active=false olur ve uygulama eskisi gibi HUD ile manuel
## calisir (yayinlanan olcum akisi degismez).

var active: bool = false      ## --n verildi mi?
var n: int = 0                ## entity sayisi
var run_index: int = 0        ## tekrar numarasi (1..R)
var out_path: String = ""     ## tek satirlik CSV'nin yazilacagi dosya
var idle_delay: float = 2.0   ## testi baslatmadan once bosta bekleme (s)
var error: String = ""        ## bos degilse argumanlar gecersiz


static func parse(args: PackedStringArray) -> RunArgs:
	var map := {}
	var i := 0
	while i < args.size():
		var a := args[i]
		if a.begins_with("--"):
			var key := a.substr(2)
			var val := ""
			var eq := key.find("=")
			if eq >= 0:
				val = key.substr(eq + 1)
				key = key.substr(0, eq)
			elif i + 1 < args.size() and not args[i + 1].begins_with("-"):
				val = args[i + 1]
				i += 1
			map[key.to_lower()] = val
		i += 1

	var r := RunArgs.new()
	if not map.has("n"):
		return r

	r.active = true
	var n_str: String = map["n"]
	var ri_str: String = map.get("run-index", "")
	var out_str: String = map.get("out", "")

	if not n_str.is_valid_int() or n_str.to_int() <= 0:
		r.error = "Gecersiz --n degeri: '%s'" % n_str
	elif not ri_str.is_valid_int() or ri_str.to_int() <= 0:
		r.error = "--run-index eksik veya gecersiz (1 veya daha buyuk tam sayi olmali)"
	elif out_str.is_empty():
		r.error = "--out eksik (sonuc CSV dosyasinin yolu)"
	elif map.has("idle-delay") and (not String(map["idle-delay"]).is_valid_float()
			or String(map["idle-delay"]).to_float() < 0.0):
		r.error = "Gecersiz --idle-delay degeri: '%s'" % map["idle-delay"]
	else:
		r.n = n_str.to_int()
		r.run_index = ri_str.to_int()
		# Windows ters bolu isaretlerini Godot'un bekledigi bicime cevir
		r.out_path = out_str.replace("\\", "/")
		if map.has("idle-delay"):
			r.idle_delay = String(map["idle-delay"]).to_float()
	return r

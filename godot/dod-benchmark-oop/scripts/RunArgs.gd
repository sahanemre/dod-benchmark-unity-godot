class_name RunArgs extends RefCounted

## S1 komut satiri argumanlari.
## Tek sorumluluk: komut satirini okumak ve dogrulamak.
##
## Godot kullanici argumanlari "--" isaretinden SONRA verilir:
##   oyun.exe -- --profile=parity --n=50000 --run-index=3 --out=C:/sonuc/x.csv
## ("--n 50000" bicimi de kabul edilir.)
##
## --profile  published | parity   (varsayilan parity)
##   published: makaledeki kosullarin aynisi
##   parity   : dort konfigurasyonda esit goruntu/render kosullari
## --dots-pipeline original | lean  (yalnizca Unity DOTS'ta anlamli; burada
##   kabul edilir ve yok sayilir, CSV'ye "na" yazilir)
## --scenario S1 | S2  (varsayilan S1; S2 ayri sahnede: --scene res://scenes/S2Main.tscn)
## --validate          (S2: sabit dt ile deterministik dogrulama kosusu)
## --frames <k>        (dogrulama kare sayisi, varsayilan 300)
##
## --n verilmezse unattended=false olur ve uygulama HUD ile manuel calisir.

## parity profilinde seed = BASE_SEED + run_index (dort konfigurasyonda ayni).
const BASE_SEED := 1000

var unattended: bool = false  ## --n verildi mi?
var n: int = 0                ## entity sayisi
var run_index: int = 0        ## tekrar numarasi (1..R); manuel modda 0
var out_path: String = ""     ## tek satirlik CSV'nin yazilacagi dosya
var idle_delay: float = 2.0   ## testi baslatmadan once bosta bekleme (s)
var profile: String = "parity"
var scenario: String = "S1"
var validate: bool = false
var frames: int = 300
var error: String = ""        ## bos degilse argumanlar gecersiz


func is_parity() -> bool:
	return profile == "parity"


func run_seed() -> int:
	return BASE_SEED + run_index


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

	# Profil manuel modda da gecerli
	if map.has("profile"):
		var prof := String(map["profile"]).to_lower()
		if prof != "published" and prof != "parity":
			r.error = "Gecersiz --profile degeri: '%s' (published | parity)" % prof
			return r
		r.profile = prof
	if map.has("scenario"):
		var sc := String(map["scenario"]).to_upper()
		if sc != "S1" and sc != "S2":
			r.error = "Gecersiz --scenario degeri: '%s' (S1 | S2)" % sc
			return r
		r.scenario = sc
	if map.has("validate"):
		r.validate = true
	if map.has("frames"):
		var fr := String(map["frames"])
		if not fr.is_valid_int() or fr.to_int() <= 0:
			r.error = "Gecersiz --frames degeri: '%s'" % fr
			return r
		r.frames = fr.to_int()
	if map.has("dots-pipeline"):
		var pipe := String(map["dots-pipeline"]).to_lower()
		if pipe != "original" and pipe != "lean":
			r.error = "Gecersiz --dots-pipeline degeri: '%s' (original | lean)" % pipe
			return r

	if not map.has("n"):
		return r

	r.unattended = true
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

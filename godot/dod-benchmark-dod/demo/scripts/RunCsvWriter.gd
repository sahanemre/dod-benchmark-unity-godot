class_name RunCsvWriter extends RefCounted

## Gozetimsiz kosunun sonucunu tek satirlik CSV olarak yazar.
## Tek sorumluluk: kosu basina sonuc dosyasi.
##
## Her kosu kendi dosyasina yazilir; bir kosu cokerse yalnizca o dosya eksik
## kalir. Once .tmp'ye yazilip sonra yeniden adlandirilir, boylece yarim
## kalmis bir dosya "tamamlanmis" sanilmaz.
## GDScript % formati her zaman nokta ondalik ayirici kullanir.

const HEADER := "engine,paradigm,scenario,N,run_index,mean_frame_time_ms,mean_fps,process_private_bytes,window_mode,render_width,render_height,dots_color,profile,dots_pipeline,focus_lost,graphics_api"
const COLUMNS := ["engine", "paradigm", "scenario", "N", "run_index", "mean_frame_time_ms",
	"mean_fps", "process_private_bytes", "window_mode", "render_width", "render_height",
	"dots_color", "profile", "dots_pipeline", "focus_lost", "graphics_api"]


## Satiri sutun sirasina gore bicimlendirir; eksik sutun varsa "" doner.
static func format_row(row: Dictionary) -> String:
	var parts := PackedStringArray()
	for col in COLUMNS:
		if not row.has(col):
			push_error("[Benchmark] CSV sutunu eksik: " + col)
			return ""
		var v = row[col]
		match col:
			"mean_frame_time_ms":
				parts.append("%.4f" % v)
			"mean_fps":
				parts.append("%.2f" % v)
			_:
				parts.append(str(v))
	return ",".join(parts)


## Basariliysa true doner.
static func write(path: String, row: Dictionary) -> bool:
	var line := format_row(row)
	if line.is_empty():
		return false

	var dir := path.get_base_dir()
	if not dir.is_empty() and not DirAccess.dir_exists_absolute(dir):
		var mk_err := DirAccess.make_dir_recursive_absolute(dir)
		if mk_err != OK:
			push_error("[Benchmark] Klasor olusturulamadi: %s (hata %d)" % [dir, mk_err])
			return false

	var tmp := path + ".tmp"
	var f := FileAccess.open(tmp, FileAccess.WRITE)
	if f == null:
		push_error("[Benchmark] Dosya acilamadi: %s (hata %d)" % [tmp, FileAccess.get_open_error()])
		return false
	f.store_string(HEADER + "\n" + line + "\n")
	f.close()

	if FileAccess.file_exists(path):
		DirAccess.remove_absolute(path)
	var err := DirAccess.rename_absolute(tmp, path)
	if err != OK:
		push_error("[Benchmark] Dosya yeniden adlandirilamadi: %s (hata %d)" % [path, err])
		return false
	return true

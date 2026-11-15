class_name RunCsvWriter extends RefCounted

## Gozetimsiz kosunun sonucunu tek satirlik CSV olarak yazar.
## Tek sorumluluk: kosu basina sonuc dosyasi.
##
## Her kosu kendi dosyasina yazilir; bir kosu cokerse yalnizca o dosya eksik
## kalir. Once .tmp'ye yazilip sonra yeniden adlandirilir, boylece yarim
## kalmis bir dosya "tamamlanmis" sanilmaz.
## GDScript % formati her zaman nokta ondalik ayirici kullanir.

const HEADER := "engine,paradigm,scenario,N,run_index,mean_frame_time_ms,mean_fps,process_private_bytes"


## Basariliysa true doner.
static func write(path: String, engine: String, paradigm: String, scenario: String,
		n: int, run_index: int, mean_frame_time_ms: float, mean_fps: float,
		private_bytes: int) -> bool:
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
	f.store_string(HEADER + "\n")
	f.store_string("%s,%s,%s,%d,%d,%.4f,%.2f,%d\n" % [
		engine, paradigm, scenario, n, run_index,
		mean_frame_time_ms, mean_fps, private_bytes
	])
	f.close()

	if FileAccess.file_exists(path):
		DirAccess.remove_absolute(path)
	var err := DirAccess.rename_absolute(tmp, path)
	if err != OK:
		push_error("[Benchmark] Dosya yeniden adlandirilamadi: %s (hata %d)" % [path, err])
		return false
	return true

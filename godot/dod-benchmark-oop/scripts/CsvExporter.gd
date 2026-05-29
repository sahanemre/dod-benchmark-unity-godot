class_name CsvExporter extends RefCounted

## Benchmark sonuclarini CSV formatina cevirir ve dosyaya yazar.
## Tek sorumluluk: serilestirme + dosya IO.
## GDScript % formati her zaman nokta ondalik ayirici kullanir (locale-bagimsiz).

const HEADER := "EntityCount,AvgFrameTime_ms,MinFrameTime_ms,MaxFrameTime_ms,StdDev_ms,AvgFPS,MinFPS,MemoryUsed_MB,TotalFrames,Duration_s"

var _content := ""


func _init() -> void:
	reset()


func reset() -> void:
	_content = HEADER + "\n"


func add_row(result: BenchmarkResult) -> void:
	_content += "%d,%.3f,%.3f,%.3f,%.3f,%.1f,%.1f,%.2f,%d,%.0f\n" % [
		result.entity_count,
		result.avg_frame_time, result.min_frame_time,
		result.max_frame_time, result.std_dev,
		result.avg_fps, result.min_fps,
		result.memory_mb, result.total_frames, result.duration
	]


## CSV'yi user:// klasorune kaydeder, dosya adini dondurur.
func save(approach_tag: String) -> String:
	var dt := Time.get_datetime_dict_from_system()
	var timestamp := "%04d-%02d-%02d_%02d-%02d-%02d" % [
		dt["year"], dt["month"], dt["day"],
		dt["hour"], dt["minute"], dt["second"]
	]
	var filename := "benchmark_%s_%s.csv" % [approach_tag, timestamp]
	var path := "user://" + filename
	var file := FileAccess.open(path, FileAccess.WRITE)
	if file:
		file.store_string(_content)
		file.close()
		print("[Benchmark] CSV kaydedildi: ", ProjectSettings.globalize_path(path))
	else:
		push_error("[Benchmark] CSV kaydedilemedi: " + path)
	return filename

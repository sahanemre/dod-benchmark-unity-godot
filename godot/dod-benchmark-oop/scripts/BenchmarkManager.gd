class_name BenchmarkManager extends Node2D

## Benchmark orkestratoru (Godot OOP).
## Tek sorumluluk: test akisini yonetmek.
## Entity spawn'i EntitySpawner'a, istatistik FrameStatistics'e,
## CSV CsvExporter'a, UI BenchmarkHUD'a devredilir.

@export var entity_counts: Array[int] = [1000, 5000, 10000, 50000, 100000]
@export var test_duration: float = 10.0
@export var move_speed: float = 300.0

var _spawner: EntitySpawner
var _stats: FrameStatistics
var _csv: CsvExporter
var _hud: BenchmarkHUD

var _is_testing: bool = false
var _test_timer: float = 0.0
var _current_test_index: int = -1
var _all_tests_mode: bool = false

var _memory_at_start: float = 0.0
var _memory_during_test: float = 0.0

var _current_fps: float = 0.0
var _current_frame_time: float = 0.0
var _status_text: String = "Hazir. Test secin ve baslatin."


func _ready() -> void:
	# VSync KAPALI + FPS sinirsiz: aksi halde frame time ekran yenileme hizina
	# kilitlenir ve gercek is yuku olculmez. Benchmark icin sart.
	DisplayServer.window_set_vsync_mode(DisplayServer.VSYNC_DISABLED)
	Engine.max_fps = 0

	var vp_size := get_viewport().get_visible_rect().size
	var screen_min := Vector2(10.0, 10.0)
	var screen_max := vp_size - Vector2(10.0, 10.0)

	_stats = FrameStatistics.new()
	_csv = CsvExporter.new()
	_spawner = EntitySpawner.new(self, move_speed, screen_min, screen_max)

	_hud = BenchmarkHUD.new()
	add_child(_hud)
	_hud.setup(entity_counts)
	_hud.start_single_pressed.connect(_on_start_single)
	_hud.start_all_pressed.connect(_on_start_all)


func _process(delta: float) -> void:
	_current_frame_time = delta * 1000.0
	_current_fps = 1.0 / delta if delta > 0.0 else 0.0

	_hud.update_metrics(_current_fps, _current_frame_time, _spawner.get_active_count())
	_hud.set_status(_status_text)

	if not _is_testing:
		return

	_test_timer += delta

	# Ilk 1 saniye warmup — veriye dahil etme
	if _test_timer > 3.0:
		_stats.add_sample(_current_frame_time)

	# Bellek olcumu (test ortasinda)
	if _test_timer > test_duration / 2.0 and _memory_during_test == 0.0:
		_memory_during_test = Performance.get_monitor(Performance.MEMORY_STATIC)

	if _test_timer >= test_duration + 3.0:
		_end_current_test()
	else:
		_status_text = "Test: %d entity | FPS: %.0f | Frame: %.2fms | Kalan: %.0fs" % [
			entity_counts[_current_test_index],
			_current_fps, _current_frame_time,
			(test_duration + 3.0 - _test_timer)
		]


func _on_start_single() -> void:
	_csv.reset()
	_start_test(_hud.get_selected_index())


func _on_start_all() -> void:
	_all_tests_mode = true
	_csv.reset()
	_start_test(0)


func _start_test(count_index: int) -> void:
	_current_test_index = count_index
	_is_testing = true
	_test_timer = 0.0
	_stats.reset()
	_memory_during_test = 0.0
	_memory_at_start = Performance.get_monitor(Performance.MEMORY_STATIC)
	_spawner.spawn(entity_counts[count_index])
	_hud.set_testing(true)
	print("[Benchmark] Test basladi: %d entity" % entity_counts[count_index])


func _end_current_test() -> void:
	_is_testing = false
	_hud.set_testing(false)

	if _stats.get_sample_count() > 0:
		var memory_mb := (_memory_during_test - _memory_at_start) / (1024.0 * 1024.0)
		if memory_mb < 0.0:
			memory_mb = _memory_during_test / (1024.0 * 1024.0)

		var result := _stats.compute(entity_counts[_current_test_index], memory_mb, test_duration)
		_csv.add_row(result)

		_status_text = "Tamamlandi: %d entity | Ort: %.2fms (%.0f FPS) | StdDev: %.2fms | Bellek: %.1f MB" % [
			result.entity_count, result.avg_frame_time, result.avg_fps,
			result.std_dev, result.memory_mb
		]
		print("[Benchmark] ", _status_text)

	if _all_tests_mode and _current_test_index < entity_counts.size() - 1:
		_start_test(_current_test_index + 1)
	else:
		_all_tests_mode = false
		_spawner.clear()
		var file := _csv.save("godot_oop")
		_status_text += "\nCSV: " + file

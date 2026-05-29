class_name BenchmarkOrchestrator extends Node2D

## Benchmark orkestratoru (Godot DOD).
## Tek sorumluluk: test akisini koordine etmek.
## Veri EntityData'da, hareket MovementProcessor'da,
## gorsel EntityRenderer'da, UI BenchmarkHUD'da yasар.

@export var entity_counts: Array[int] = [1000, 5000, 10000, 50000, 100000]
@export var test_duration: float = 10.0
@export var move_speed: float = 300.0

var _data: EntityData
var _renderer: EntityRenderer
var _stats: FrameStatistics
var _csv: CsvExporter
var _hud: BenchmarkHUD

var _is_testing: bool = false
var _test_timer: float = 0.0
var _current_test_index: int = -1
var _all_tests_mode: bool = false
var _screen_min: Vector2
var _screen_max: Vector2

var _memory_at_start: float = 0.0
var _memory_during_test: float = 0.0
var _current_fps: float = 0.0
var _current_frame_time: float = 0.0
var _status_text: String = "Hazir. Test secin ve baslatin."


func _ready() -> void:
	var vp_size := get_viewport().get_visible_rect().size
	_screen_min = Vector2(10.0, 10.0)
	_screen_max = vp_size - Vector2(10.0, 10.0)

	_data = EntityData.new()

	_renderer = EntityRenderer.new()
	_renderer.set_data(_data)
	add_child(_renderer)

	_stats = FrameStatistics.new()
	_csv = CsvExporter.new()

	_hud = BenchmarkHUD.new()
	add_child(_hud)
	_hud.setup(entity_counts)
	_hud.start_single_pressed.connect(_on_start_single)
	_hud.start_all_pressed.connect(_on_start_all)


func _process(delta: float) -> void:
	_current_frame_time = delta * 1000.0
	_current_fps = 1.0 / delta if delta > 0.0 else 0.0

	_hud.update_metrics(_current_fps, _current_frame_time, _data.count)
	_hud.set_status(_status_text)

	if not _is_testing:
		return

	# Hareket guncelleme — MovementProcessor sistemi cagrilir
	MovementProcessor.process(_data, delta, _screen_min, _screen_max)

	_test_timer += delta

	# 3 saniyelik warmup — veriye dahil etme
	if _test_timer > 3.0:
		_stats.add_sample(_current_frame_time)

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
	EntityFactory.spawn(_data, entity_counts[count_index], move_speed, _screen_min, _screen_max)
	_hud.set_testing(true)
	print("[DOD Benchmark] Test basladi: %d entity" % entity_counts[count_index])


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
		print("[DOD Benchmark] ", _status_text)

	if _all_tests_mode and _current_test_index < entity_counts.size() - 1:
		_start_test(_current_test_index + 1)
	else:
		_all_tests_mode = false
		EntityFactory.clear(_data)
		var file := _csv.save("godot_dod")
		_status_text += "\nCSV: " + file

class_name BenchmarkManager extends Node2D

## Benchmark orkestratoru (Godot OOP).
## Tek sorumluluk: test akisini yonetmek.
## Entity spawn'i EntitySpawner'a, istatistik FrameStatistics'e,
## CSV CsvExporter'a, UI BenchmarkHUD'a devredilir. Gozetimsiz S1 kosusunun
## olcum disi kismi S1RunSession'dadir.

@export var entity_counts: Array[int] = [1000, 5000, 10000, 50000, 100000]
@export var test_duration: float = 10.0
@export var move_speed: float = 300.0

var _spawner: EntitySpawner
var _stats: FrameStatistics
var _csv: CsvExporter
var _hud: BenchmarkHUD = null   ## parity gozetimsiz kosuda null (HUD kapali)
var _session: S1RunSession

var _is_testing: bool = false
var _test_timer: float = 0.0
var _current_test_index: int = -1
var _all_tests_mode: bool = false

# Eski bellek olcumu (motor sayaci; yalnizca HUD/elle kullanimda)
var _memory_at_start: float = 0.0
var _memory_during_test: float = 0.0

var _current_fps: float = 0.0
var _current_frame_time: float = 0.0
var _status_text: String = "Hazir. Test secin ve baslatin."


func _ready() -> void:
	_session = S1RunSession.new("oop", "[Benchmark]", get_tree())
	# Pencere ve olcekleme once: dunya sinirlari asagida gorunur alandan hesaplanir
	_session.apply_display(get_window())

	# VSync KAPALI + FPS sinirsiz: aksi halde frame time ekran yenileme hizina
	# kilitlenir ve gercek is yuku olculmez. Benchmark icin sart.
	DisplayServer.window_set_vsync_mode(DisplayServer.VSYNC_DISABLED)
	Engine.max_fps = 0

	var vp_size := get_viewport().get_visible_rect().size
	var screen_min := Vector2(10.0, 10.0)
	var screen_max := vp_size - Vector2(10.0, 10.0)

	# Entity'nin cizim boyutu (yalnizca gorunum; hareket mantigi degismez)
	EntityMover.draw_size = _session.entity_size()

	_stats = FrameStatistics.new()
	_csv = CsvExporter.new()
	_spawner = EntitySpawner.new(self, move_speed, screen_min, screen_max)

	if _session.hud_enabled():
		_hud = BenchmarkHUD.new()
		add_child(_hud)
		_hud.setup(entity_counts)
		_hud.start_single_pressed.connect(_on_start_single)
		_hud.start_all_pressed.connect(_on_start_all)

	if _session.setup(entity_counts) and _session.waiting_to_start():
		_status_text = "Gozetimsiz kosu: N=%d, run=%d" % [_session.args.n, _session.args.run_index]


func _notification(what: int) -> void:
	if what == NOTIFICATION_APPLICATION_FOCUS_OUT and _session != null:
		_session.on_focus_out(_is_testing)


func _process(delta: float) -> void:
	_current_frame_time = delta * 1000.0
	_current_fps = 1.0 / delta if delta > 0.0 else 0.0

	if _hud != null:
		_hud.update_metrics(_current_fps, _current_frame_time, _spawner.get_active_count())
		_hud.set_status(_status_text)

	# Gozetimsiz mod: butona basmanin yerine, motor acilisindan sonra kisa bir
	# bekleme ile testi baslatir. Manuel akista buton sinyali _process'ten
	# ONCE islenir ve ayni frame'in _process'i zamanlayiciyi ilerletir; ayni
	# zamanlamayi korumak icin baslattiktan sonra asagiya devam ediyoruz.
	if _session.waiting_to_start():
		if not _session.tick_idle(delta):
			return
		_csv.reset()
		_start_test(_session.unattended_index)

	if not _is_testing:
		return

	_test_timer += delta

	# Warmup'in son ~100 ms'sinde OS bellegi + goruntu bilgisi (gozetimsiz kosu)
	_session.after_timer_advance(_test_timer, delta, get_window())

	# Ilk 3 saniye warmup — veriye dahil etme
	if _test_timer > 3.0:
		_stats.add_sample(_current_frame_time)

	# Eski bellek olcumu (motor sayaci, test ortasinda). Gozetimsiz kosuda
	# kapali: olcum penceresinde ornekleme yapilmaz.
	if not _session.started and _test_timer > test_duration / 2.0 and _memory_during_test == 0.0:
		_memory_during_test = Performance.get_monitor(Performance.MEMORY_STATIC)

	if _test_timer >= test_duration + 3.0:
		_end_current_test()
	elif _hud != null:
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
	if _session.started:
		_session.on_test_started(get_window())
	else:
		_memory_at_start = Performance.get_monitor(Performance.MEMORY_STATIC)

	# parity: tekrarlanabilir baslangic (pozisyon, hiz, renk) — seed = 1000 + run_index.
	# published: orijinal davranis (Godot'un acilista rastgele seed'i).
	if _session.is_parity():
		seed(_session.args.run_seed())

	_spawner.spawn(entity_counts[count_index])
	if _hud != null:
		_hud.set_testing(true)
	print("[Benchmark] Test basladi: %d entity" % entity_counts[count_index])


func _end_current_test() -> void:
	_is_testing = false
	if _hud != null:
		_hud.set_testing(false)

	if _session.started:
		_session.finish(_stats, test_duration, "na", "na")
		return

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

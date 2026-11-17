extends Node2D

## Benchmark orkestratoru (Godot DOD — native C++ hybrid).
## Tek sorumluluk: test akisini koordine etmek. Gozetimsiz S1 kosusunun olcum
## disi kismi S1RunSession'dadir.
##
## Hybrid mimari:
##   - HAREKET (SoA hot loop) native C++ tarafinda (MovementWorld eklentisi)
##   - RENDER  GDScript + MultiMesh: native her frame MultiMesh buffer'i uretir,
##             tek MultiMeshInstance2D tum entity'leri tek draw call ile cizer.
##             Buffer'da entity basina renk de vardir (use_colors) ve her frame
##             yeniden yuklenir; bu bilincli olarak degistirilmedi.
##
## NOT: MovementWorld native sinifi yalnizca GDExtension derlenip
## demo/bin/ icine konunca var olur. Derlenmemisse panel kilitlenir
## ve build talimati gosterilir (bkz. ../README.md).

@export var entity_counts: Array[int] = [1000, 5000, 10000, 50000, 100000]
@export var test_duration: float = 10.0
@export var move_speed: float = 300.0

var _world: RefCounted = null         # MovementWorld (native)
var _multimesh: MultiMesh
var _mm_instance: MultiMeshInstance2D
var _stats: FrameStatistics
var _csv: CsvExporter
var _hud: BenchmarkHUD = null         # parity gozetimsiz kosuda null (HUD kapali)
var _session: S1RunSession

var _native_ok: bool = false
var _is_testing: bool = false
var _test_timer: float = 0.0
var _current_test_index: int = -1
var _all_tests_mode: bool = false
var _screen_min: Vector2
var _screen_max: Vector2

# Eski bellek olcumu (motor sayaci; yalnizca HUD/elle kullanimda)
var _memory_at_start: float = 0.0
var _memory_during_test: float = 0.0
var _current_fps: float = 0.0
var _current_frame_time: float = 0.0
var _status_text: String = "Hazir. Test secin ve baslatin."


func _ready() -> void:
	_session = S1RunSession.new("dod", "[DOD Native]", get_tree())
	# Pencere ve olcekleme once: dunya sinirlari asagida gorunur alandan hesaplanir
	_session.apply_display(get_window())

	# VSync KAPALI + FPS sinirsiz: aksi halde frame time ekran yenileme hizina
	# kilitlenir (orn. tum testler 165 FPS / 6.06ms, StdDev 0) ve gercek is
	# yuku olculmez. Benchmark icin sart.
	DisplayServer.window_set_vsync_mode(DisplayServer.VSYNC_DISABLED)
	Engine.max_fps = 0

	var vp_size := get_viewport().get_visible_rect().size
	_screen_min = Vector2(10.0, 10.0)
	_screen_max = vp_size - Vector2(10.0, 10.0)

	_stats = FrameStatistics.new()
	_csv = CsvExporter.new()

	# MultiMesh kurulumu (2B, instance renkleri acik)
	_multimesh = MultiMesh.new()
	_multimesh.transform_format = MultiMesh.TRANSFORM_2D
	_multimesh.use_colors = true
	var quad := QuadMesh.new()
	var s := _session.entity_size()    # published 8 px, parity 6,75 px (yalnizca gorunum)
	quad.size = Vector2(s, s)
	_multimesh.mesh = quad
	_mm_instance = MultiMeshInstance2D.new()
	_mm_instance.multimesh = _multimesh
	add_child(_mm_instance)

	if _session.hud_enabled():
		_hud = BenchmarkHUD.new()
		add_child(_hud)
		_hud.setup(entity_counts)
		_hud.start_single_pressed.connect(_on_start_single)
		_hud.start_all_pressed.connect(_on_start_all)

	# Native eklenti var mi?
	if ClassDB.class_exists("MovementWorld"):
		_world = ClassDB.instantiate("MovementWorld")
		_native_ok = _world != null

	if not _native_ok:
		_status_text = ("HATA: MovementWorld native eklentisi yuklenemedi.\n"
			+ "GDExtension'i derleyip demo/bin/ icine koyun (bkz. README.md):\n"
			+ "  scons platform=windows target=template_debug")
		if _hud != null:
			_hud.set_unavailable(_status_text)

	if not _session.setup(entity_counts) or not _session.waiting_to_start():
		return
	if not _native_ok:
		_session.fail(S1RunSession.EXIT_SETUP_FAILED, "MovementWorld native eklentisi yuklenemedi.")
		return
	if _session.is_parity() and not _world.has_method("set_seed"):
		_session.fail(S1RunSession.EXIT_BUILD_MISMATCH,
			"parity: MovementWorld.set_seed yok; template_release DLL'i guncel kaynaktan derlenmeli.")
		return
	_status_text = "Gozetimsiz kosu: N=%d, run=%d" % [_session.args.n, _session.args.run_index]


func _notification(what: int) -> void:
	if what == NOTIFICATION_APPLICATION_FOCUS_OUT and _session != null:
		_session.on_focus_out(_is_testing)


func _process(delta: float) -> void:
	_current_frame_time = delta * 1000.0
	_current_fps = 1.0 / delta if delta > 0.0 else 0.0

	if _hud != null:
		var active: int = _world.get_count() if _native_ok else 0
		_hud.update_metrics(_current_fps, _current_frame_time, active)
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

	# HAREKET: native SoA toplu guncelleme
	_world.update(delta, _screen_min, _screen_max)
	# RENDER: native buffer'i MultiMesh'e aktar (tek draw call)
	_multimesh.buffer = _world.get_buffer()

	_test_timer += delta

	# Warmup'in son ~100 ms'sinde OS bellegi + goruntu bilgisi (gozetimsiz kosu)
	_session.after_timer_advance(_test_timer, delta, get_window())

	# 3 saniyelik warmup — veriye dahil etme
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
	if not _native_ok: return
	_csv.reset()
	_start_test(_hud.get_selected_index())


func _on_start_all() -> void:
	if not _native_ok: return
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
	# published: orijinal davranis (C++ tarafindaki sabit baslangic durumu).
	if _session.is_parity():
		_world.set_seed(_session.args.run_seed())

	_world.spawn(entity_counts[count_index], move_speed, _screen_min, _screen_max)
	_multimesh.instance_count = entity_counts[count_index]

	if _hud != null:
		_hud.set_testing(true)
	print("[DOD Native] Test basladi: %d entity" % entity_counts[count_index])


func _end_current_test() -> void:
	_is_testing = false
	if _hud != null:
		_hud.set_testing(false)

	if _session.started:
		_session.finish(_stats, test_duration, "na", "na")
		return

	if _stats.get_sample_count() > 0:
		# NOT: Performance.MEMORY_STATIC native std::vector tahsislerini
		# kapsamaz (Godot disi bellek). Bu deger Godot tarafi icin gecerlidir;
		# native bellek karsilastirmasi icin harici profiler gerekir.
		var memory_mb := (_memory_during_test - _memory_at_start) / (1024.0 * 1024.0)
		if memory_mb < 0.0:
			memory_mb = _memory_during_test / (1024.0 * 1024.0)

		var result := _stats.compute(entity_counts[_current_test_index], memory_mb, test_duration)
		_csv.add_row(result)

		_status_text = "Tamamlandi: %d entity | Ort: %.2fms (%.0f FPS) | StdDev: %.2fms | Bellek: %.1f MB" % [
			result.entity_count, result.avg_frame_time, result.avg_fps,
			result.std_dev, result.memory_mb
		]
		print("[DOD Native] ", _status_text)

	if _all_tests_mode and _current_test_index < entity_counts.size() - 1:
		_start_test(_current_test_index + 1)
	else:
		_all_tests_mode = false
		_world.clear()
		_multimesh.instance_count = 0
		var file := _csv.save("godot_dod_native")
		_status_text += "\nCSV: " + file

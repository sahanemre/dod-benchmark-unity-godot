class_name S1RunSession extends RefCounted

## S1 gozetimsiz kosu akisi (Godot OOP ve Godot DOD'da birebir ayni dosya).
## Tek sorumluluk: kosunun olcum disi kismini yonetmek: argumanlar ve profil,
## goruntu kosullari, build kontrolu, bosta bekleme, warmup sonu bellek +
## goruntu kaydi, odak takibi ve tek satirlik CSV.
##
## Olcum davranisina (frame time ornekleme, 3 s warmup + 10 s olcum, hareket)
## dokunmaz; bunlar yoneticide (BenchmarkManager / BenchmarkOrchestrator) kalir.
##
## Cikis kodlari:
##   0 basarili, 2 gecersiz arguman, 3 ornek yok, 4 bellek okunamadi,
##   5 yazma hatasi, 6 native eklenti (MovementWorld) yok, 7 procmem yok,
##   8 goruntu modu/cozunurluk beklenenden farkli, 10 build profille uyusmuyor.

const EXIT_OK := 0
const EXIT_BAD_ARGS := 2
const EXIT_NO_SAMPLES := 3
const EXIT_NO_MEMORY := 4
const EXIT_WRITE_FAILED := 5
const EXIT_SETUP_FAILED := 6
const EXIT_PROCMEM_MISSING := 7
const EXIT_DISPLAY_MISMATCH := 8
const EXIT_BUILD_MISMATCH := 10

const WARMUP_SECONDS := 3.0
## Projenin (ve makalenin) mantiksal dunyasi; published penceresi de bu boyutta.
const BASE_SIZE := Vector2i(1280, 720)
const PARITY_SIZE := Vector2i(1920, 1080)

var args: RunArgs
var unattended_index: int = -1
var started: bool = false

var _paradigm: String
var _tag: String
var _tree: SceneTree
var _mem: ProcessMemoryReader
var _idle_timer := 0.0

# Warmup sonunda bir kez kaydedilenler
var _snapshot_taken := false
var _private_bytes := -1
var _window_mode := "unknown"
var _render_size := Vector2i.ZERO
var _display_ok := false

var _focus_lost := false
var _driver := ""


func _init(paradigm: String, tag: String, tree: SceneTree) -> void:
	_paradigm = paradigm
	_tag = tag
	_tree = tree
	args = RunArgs.parse(OS.get_cmdline_user_args())
	_mem = ProcessMemoryReader.new()


func is_parity() -> bool:
	return args.is_parity()


## HUD: published profilinde ve elle kullanimda acik; parity gozetimsiz kosuda kapali.
func hud_enabled() -> bool:
	return not (args.unattended and args.is_parity())


## Gozetimsiz kosu bekleniyor ama test henuz baslamadi.
func waiting_to_start() -> bool:
	return unattended_index >= 0 and not started


## Profile gore pencere.
## published: proje varsayilani = makale kosulu (1280x720 pencere, stretch yok).
## parity   : exclusive fullscreen (masaustu cozunurlugu, 1920x1080).
##            canvas_items/keep ile 1280x720 mantiksal dunya aynen kalir ve
##            ekrana 1,5 kat olceklenir; dunya sinirlari ve hizlar degismez.
func apply_display(window: Window) -> void:
	if not is_parity():
		return
	window.content_scale_size = BASE_SIZE
	window.content_scale_aspect = Window.CONTENT_SCALE_ASPECT_KEEP
	window.content_scale_mode = Window.CONTENT_SCALE_MODE_CANVAS_ITEMS
	window.mode = Window.MODE_EXCLUSIVE_FULLSCREEN


## Entity'nin mantiksal boyutu (px). parity: 6,75 x 1,5 = 10,125 px ekranda,
## Unity'deki 0,15 birimlik entity ile ayni (1080p, orthographic size 8).
func entity_size() -> float:
	return 6.75 if is_parity() else 8.0


func expected_mode() -> int:
	return Window.MODE_EXCLUSIVE_FULLSCREEN if is_parity() else Window.MODE_WINDOWED


func expected_size() -> Vector2i:
	return PARITY_SIZE if is_parity() else BASE_SIZE


static func mode_name(mode: int) -> String:
	match mode:
		Window.MODE_EXCLUSIVE_FULLSCREEN:
			return "exclusive"
		Window.MODE_FULLSCREEN:
			return "borderless"
		Window.MODE_WINDOWED:
			return "windowed"
		Window.MODE_MAXIMIZED:
			return "maximized"
		Window.MODE_MINIMIZED:
			return "minimized"
	return "unknown"


## _ready'de cagrilir. Gecersiz durumda uygulamayi hata koduyla kapatir ve
## false doner. Elle acilista (--n yok) yalnizca profil bilgisini loglar.
func setup(entity_counts: Array[int]) -> bool:
	var driver := RenderingServer.get_current_rendering_driver_name()
	_driver = driver
	print("%s profile=%s display=%s %dx%d driver=%s debug_build=%s" % [
		_tag, args.profile, mode_name(expected_mode()), expected_size().x, expected_size().y,
		driver, OS.is_debug_build()])

	if not args.error.is_empty():
		return fail(EXIT_BAD_ARGS, args.error)
	if not args.unattended:
		return true

	if not _mem.is_available():
		return fail(EXIT_PROCMEM_MISSING, "procmem eklentisi yuklenemedi (bin/libprocmem*.dll).")

	if is_parity():
		if OS.is_debug_build():
			return fail(EXIT_BUILD_MISMATCH,
				"parity: release export sablonu bekleniyordu (Export With Debug kapali).")
		if driver != "d3d12":
			return fail(EXIT_BUILD_MISMATCH, "parity: d3d12 bekleniyordu, aktif surucu: " + driver)
	elif not OS.is_debug_build():
		return fail(EXIT_BUILD_MISMATCH,
			"published: debug export sablonu bekleniyordu (makaledeki gibi Export With Debug acik).")

	var index := entity_counts.find(args.n)
	if index < 0:
		return fail(EXIT_BAD_ARGS, "N=%d entity_counts listesinde yok." % args.n)

	unattended_index = index
	print("%s Gozetimsiz kosu: N=%d, run=%d, seed=%s -> %s" % [
		_tag, args.n, args.run_index,
		str(args.run_seed()) if is_parity() else "orijinal", args.out_path])
	return true


## Hata loglar, gozetimsiz akisi durdurur ve uygulamayi kapatir.
func fail(code: int, message: String) -> bool:
	push_error(_tag + " " + message)
	unattended_index = -1
	_tree.quit(code)
	return false


## Bosta bekler; testi baslatma zamani geldiginde bir kez true doner.
func tick_idle(dt: float) -> bool:
	if not waiting_to_start():
		return false
	_idle_timer += dt
	if _idle_timer < args.idle_delay:
		return false
	started = true
	return true


## Test baslarken (spawn'dan once) cagrilir.
func on_test_started(window: Window) -> void:
	_snapshot_taken = false
	_private_bytes = -1
	_focus_lost = not window.has_focus()


func on_focus_out(testing: bool) -> void:
	if testing and started:
		_focus_lost = true


## Her frame, test zamanlayicisi ilerletildikten sonra cagrilir.
## Bellek ve goruntu bilgisi warmup'in son ~100 ms'sinde (en az ~1,5 frame
## kala) bir kez okunur. Frame time ornekleri kesintisiz oldugu icin bir
## frame'de yapilan is bir SONRAKI frame'in suresine yansir; okumayi warmup
## bitmeden yapmak bu maliyetin atilan bir warmup frame'ine dusmesini saglar.
## Hicbir frame bu araliga dusmezse olcum penceresinin ilk frame'inde okunur.
func after_timer_advance(test_timer: float, dt: float, window: Window) -> void:
	if _snapshot_taken or not started:
		return
	var slot := maxf(0.1, 3.0 * dt)
	var margin := 1.5 * dt
	var in_slot := test_timer >= WARMUP_SECONDS - slot and test_timer <= WARMUP_SECONDS - margin
	var late := test_timer > WARMUP_SECONDS
	if not in_slot and not late:
		return
	if late:
		push_warning(_tag + " Bellek warmup icinde okunamadi; olcum penceresinin ilk frame'inde okundu.")

	_private_bytes = _mem.get_private_bytes()
	_window_mode = mode_name(window.mode)
	_render_size = window.size
	_display_ok = window.mode == expected_mode() and window.size == expected_size()
	_snapshot_taken = true


## Kosunun sonu: tek satiri yazar ve uygulamayi kapatir.
## collision_pairs_mean >= 0 ise (S2) CSV'ye 17. sutun olarak yazilir.
func finish(stats: FrameStatistics, test_duration: float, dots_color: String,
		dots_pipeline: String, collision_pairs_mean: float = -1.0) -> void:
	var code := EXIT_OK

	if stats.get_sample_count() == 0:
		push_error(_tag + " Olcum penceresinde hic ornek yok.")
		code = EXIT_NO_SAMPLES
	elif _private_bytes < 0:
		push_error(_tag + " Process private bytes okunamadi.")
		code = EXIT_NO_MEMORY
	elif not _display_ok:
		push_error("%s Goruntu kosulu tutmuyor: beklenen %s %dx%d, olculen %s %dx%d." % [
			_tag, mode_name(expected_mode()), expected_size().x, expected_size().y,
			_window_mode, _render_size.x, _render_size.y])
		code = EXIT_DISPLAY_MISMATCH
	else:
		var result := stats.compute(args.n, 0.0, test_duration)
		var row := {
			"engine": "godot",
			"paradigm": _paradigm,
			"scenario": args.scenario,
			"N": args.n,
			"run_index": args.run_index,
			"mean_frame_time_ms": result.avg_frame_time,
			"mean_fps": result.avg_fps,
			"process_private_bytes": _private_bytes,
			"window_mode": _window_mode,
			"render_width": _render_size.x,
			"render_height": _render_size.y,
			"dots_color": dots_color,
			"profile": args.profile,
			"dots_pipeline": dots_pipeline,
			"focus_lost": 1 if _focus_lost else 0,
			"graphics_api": _driver,
		}
		if collision_pairs_mean >= 0.0:
			row[RunCsvWriter.S2_COLUMN] = collision_pairs_mean
		if RunCsvWriter.write(args.out_path, row):
			print(_tag, " Kosu tamamlandi: ", RunCsvWriter.format_row(row))
			if _focus_lost:
				push_warning(_tag + " Kosu sirasinda pencere odagi kaybedildi (focus_lost=1).")
		else:
			code = EXIT_WRITE_FAILED

	_tree.quit(code)

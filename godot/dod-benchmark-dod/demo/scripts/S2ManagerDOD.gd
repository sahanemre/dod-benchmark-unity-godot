extends Node2D

## S2 (carpisma) yoneticisi — Godot DOD (native C++).
## Ayri sahne: res://scenes/S2Main.tscn (calistirma: --scene res://scenes/S2Main.tscn).
## S1 sahnesine ve BenchmarkOrchestrator'a dokunmaz.
##
## Her kare (tek C++ cagrisi zinciri, bu sirayla):
##   1. Hareket: MovementWorld.update (S1 ile ayni kod)
##   2-5. MovementWorld.collide: uniform grid (counting sort), komsu hucre
##        testi (j > i), elastik tepki, kesisen cift sayisi
## Veri SoA dizilerinde, grid entity INDEKSI tutar.
##
## Modlar:
##   --validate : sabit dt (1/60) ile --frames kare; kare basina cift sayilari
##                dosyaya yazilir (N <= 2000'de O(N^2) kontrol sutunu ile).
##   aksi halde : S1 ile ayni olcum (bosta bekleme, 3 s warmup + 10 s olcum);
##                CSV'ye collision_pairs_mean eklenir.

@export var entity_counts: Array[int] = [1000, 5000, 10000, 50000, 100000]
@export var test_duration: float = 10.0

var _world: RefCounted = null         # MovementWorld (native)
var _multimesh: MultiMesh
var _session: S1RunSession
var _stats: FrameStatistics

var _is_testing := false
var _test_timer := 0.0
var _pairs_sum := 0.0
var _pairs_frames := 0

# Dogrulama
var _validating := false
var _frame := 0
var _lines := PackedStringArray()
var _bruteforce := false


func _ready() -> void:
	_session = S1RunSession.new("dod", "[S2 DOD]", get_tree())
	_session.apply_display(get_window())
	DisplayServer.window_set_vsync_mode(DisplayServer.VSYNC_DISABLED)
	Engine.max_fps = 0

	_stats = FrameStatistics.new()

	_multimesh = MultiMesh.new()
	_multimesh.transform_format = MultiMesh.TRANSFORM_2D
	_multimesh.use_colors = true
	var quad := QuadMesh.new()
	quad.size = Vector2(2.0 * S2Params.RADIUS, 2.0 * S2Params.RADIUS)
	_multimesh.mesh = quad
	var mm := MultiMeshInstance2D.new()
	mm.multimesh = _multimesh
	add_child(mm)

	if ClassDB.class_exists("MovementWorld"):
		_world = ClassDB.instantiate("MovementWorld")

	if not _session.setup(entity_counts):
		return
	var args := _session.args
	if args.scenario != "S2" or not args.unattended or not args.is_parity():
		_session.fail(S1RunSession.EXIT_BAD_ARGS,
			"S2 sahnesi yalnizca --scenario=S2, --profile=parity ve --n ile calisir.")
		return
	if _world == null or not _world.has_method("collide"):
		_session.fail(S1RunSession.EXIT_SETUP_FAILED,
			"MovementWorld (S2 metotlariyla) yuklenemedi; template_release DLL'i guncel kaynaktan derlenmeli.")
		return

	if args.validate:
		_validating = true
		_bruteforce = args.n <= S2Params.VALIDATE_BRUTEFORCE_MAX_N
		_spawn()
		print("[S2 DOD] Dogrulama: N=%d, seed=%d, %d kare, dt=1/60%s" % [
			args.n, args.run_seed(), args.frames, ", O(N^2) kontrol" if _bruteforce else ""])


func _notification(what: int) -> void:
	if what == NOTIFICATION_APPLICATION_FOCUS_OUT and _session != null:
		_session.on_focus_out(_is_testing)


func _spawn() -> void:
	var n := _session.args.n
	_world.spawn_s2(n, S2Params.SPEED, S2Params.WORLD_MIN, S2Params.WORLD_MAX, _session.args.run_seed())
	_multimesh.instance_count = n


## Bir kare: hareket + carpisma. Kesisen cift sayisini dondurur.
func _step(dt: float) -> int:
	_world.update(dt, S2Params.WORLD_MIN, S2Params.WORLD_MAX)
	var pairs: int = _world.collide(S2Params.WORLD_MIN, S2Params.WORLD_MAX, S2Params.CELL)
	_multimesh.buffer = _world.get_buffer()
	return pairs


func _process(delta: float) -> void:
	if _validating:
		_validate_frame()
		return

	if _session.waiting_to_start():
		if not _session.tick_idle(delta):
			return
		_start_test()

	if not _is_testing:
		return

	var pairs := _step(delta)
	_test_timer += delta
	_session.after_timer_advance(_test_timer, delta, get_window())

	# Ilk 3 saniye warmup — veriye dahil etme
	if _test_timer > 3.0:
		_stats.add_sample(delta * 1000.0)
		_pairs_sum += pairs
		_pairs_frames += 1

	if _test_timer >= test_duration + 3.0:
		_is_testing = false
		var mean := _pairs_sum / _pairs_frames if _pairs_frames > 0 else 0.0
		_session.finish(_stats, test_duration, "na", "na", mean)


func _start_test() -> void:
	_is_testing = true
	_test_timer = 0.0
	_stats.reset()
	_pairs_sum = 0.0
	_pairs_frames = 0
	_session.on_test_started(get_window())
	_spawn()
	print("[S2 DOD] Test basladi: %d entity, seed=%d" % [_session.args.n, _session.args.run_seed()])


func _validate_frame() -> void:
	_frame += 1
	_world.update(S2Params.FIXED_DT, S2Params.WORLD_MIN, S2Params.WORLD_MAX)
	# O(N^2) sayim, tepkiden ONCE ayni pozisyonlarda (referansla ayni)
	var bf: int = _world.count_pairs_bruteforce(S2Params.CELL) if _bruteforce else 0
	var pairs: int = _world.collide(S2Params.WORLD_MIN, S2Params.WORLD_MAX, S2Params.CELL)
	var responses: int = _world.get_last_responses()
	_multimesh.buffer = _world.get_buffer()
	if _bruteforce:
		_lines.append("%d,%d,%d,%d" % [_frame, pairs, responses, bf])
	else:
		_lines.append("%d,%d,%d" % [_frame, pairs, responses])

	if _frame >= _session.args.frames:
		_validating = false
		var ok := S2Validation.write(_session.args.out_path, _lines, _bruteforce)
		print("[S2 DOD] Dogrulama bitti: %d kare -> %s" % [_frame, _session.args.out_path])
		get_tree().quit(0 if ok else S1RunSession.EXIT_WRITE_FAILED)

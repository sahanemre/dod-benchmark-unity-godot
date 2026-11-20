extends Node2D

## S2 (carpisma) yoneticisi — Godot OOP (GDScript).
## Ayri sahne: res://scenes/S2Main.tscn (calistirma: --scene res://scenes/S2Main.tscn).
## S1 sahnesine ve BenchmarkManager'a dokunmaz.
##
## Her kare, bu sirayla:
##   1. Hareket: her EntityS2 kendi _process'inde (S1 ile ayni mantik).
##   2-5. Bu yonetici (process_priority = 1, entity'lerden SONRA calisir):
##        uniform grid (counting sort; grid nesne REFERANSLARI tutar), komsu
##        hucre testi (j > i), elastik tepki, kesisen cift sayisi.
## Pozisyon/hiz her entity'nin kendi nesnesinde; carpisma testi referans
## uzerinden okur/yazar.
##
## Modlar: --validate (sabit dt, kare basina cift sayilari dosyaya) ya da
## S1 ile ayni olcum (CSV'ye collision_pairs_mean eklenir).

@export var entity_counts: Array[int] = [1000, 5000, 10000, 50000, 100000]
@export var test_duration: float = 10.0

var _session: S1RunSession
var _stats: FrameStatistics

var _entities: Array[EntityS2] = []    ## spawn sirasinda (index = i)
var _sorted: Array[EntityS2] = []      ## grid: hucreye gore sirali REFERANSLAR
var _cell_start := PackedInt32Array()  ## hucre sayisi + 1 (prefix sum)
var _cell_fill := PackedInt32Array()
var _cols := 0
var _rows := 0
var _ncells := 0
var _last_steps := 0
var _last_responses := 0

var _is_testing := false
var _test_timer := 0.0
var _pairs_sum := 0.0
var _pairs_frames := 0

var _validating := false
var _frame := 0
var _lines := PackedStringArray()


func _ready() -> void:
	# Entity'lerin _process'i (oncelik 0) bitince carpisma adimi calissin
	process_priority = 1

	_session = S1RunSession.new("oop", "[S2 OOP]", get_tree())
	_session.apply_display(get_window())
	DisplayServer.window_set_vsync_mode(DisplayServer.VSYNC_DISABLED)
	Engine.max_fps = 0
	_stats = FrameStatistics.new()

	_cols = int(ceil((S2Params.WORLD_MAX.x - S2Params.WORLD_MIN.x) / S2Params.CELL))
	_rows = int(ceil((S2Params.WORLD_MAX.y - S2Params.WORLD_MIN.y) / S2Params.CELL))
	_ncells = _cols * _rows
	_cell_start.resize(_ncells + 1)
	_cell_fill.resize(_ncells)
	EntityS2.draw_size = 2.0 * S2Params.RADIUS

	if not _session.setup(entity_counts):
		return
	var args := _session.args
	if args.scenario != "S2" or not args.unattended or not args.is_parity():
		_session.fail(S1RunSession.EXIT_BAD_ARGS,
			"S2 sahnesi yalnizca --scenario=S2, --profile=parity ve --n ile calisir.")
		return

	if args.validate:
		_validating = true
		EntityS2.fixed_dt = S2Params.FIXED_DT
		_spawn()
		print("[S2 OOP] Dogrulama: N=%d, seed=%d, %d kare, dt=1/60" % [
			args.n, args.run_seed(), args.frames])


func _notification(what: int) -> void:
	if what == NOTIFICATION_APPLICATION_FOCUS_OUT and _session != null:
		_session.on_focus_out(_is_testing)


## Ortak RNG ile spawn (entity basina 6 cekilis: x, y, aci, r, g, b)
func _spawn() -> void:
	var n := _session.args.n
	var rng := S2Rng.new(_session.args.run_seed())
	_entities.resize(n)
	_sorted.resize(n)
	for i in range(n):
		var p := rng.range2(S2Params.WORLD_MIN, S2Params.WORLD_MAX)
		var a := rng.range1(0.0, S2Params.TWO_PI)
		var v := Vector2(cos(a), sin(a)) * S2Params.SPEED
		var col := Color(rng.range1(0.3, 1.0), rng.range1(0.3, 1.0), rng.range1(0.3, 1.0))
		var e := EntityS2.new()
		add_child(e)
		e.setup(i, p, v, col, S2Params.WORLD_MIN, S2Params.WORLD_MAX)
		_entities[i] = e
	_last_steps = 0


## Entity'ler bu karede hareket ettiyse carpisma adimini calistirir.
## Doner: kesisen cift sayisi, ya da -1 (bu karede hareket olmadi).
func _step_if_moved() -> int:
	if _entities.is_empty() or _entities[0].steps == _last_steps:
		return -1
	_last_steps = _entities[0].steps
	return _collide()


func _collide() -> int:
	var n := _entities.size()
	var min_v := S2Params.WORLD_MIN

	# Broad-phase: counting sort (sayim -> prefix sum -> referans dizisi).
	# Hucre koordinati Vector2 (float32) ile hesaplanir: C#/C++ ile ayni yuvarlama.
	_cell_start.fill(0)
	for e in _entities:
		var g := (e.pos - min_v) / S2Params.CELL
		var cx := clampi(int(g.x), 0, _cols - 1)
		var cy := clampi(int(g.y), 0, _rows - 1)
		e.cell = cy * _cols + cx
		_cell_start[e.cell + 1] += 1
	for c in range(_ncells):
		_cell_start[c + 1] += _cell_start[c]
	for c in range(_ncells):
		_cell_fill[c] = _cell_start[c]
	for e in _entities:
		_sorted[_cell_fill[e.cell]] = e
		_cell_fill[e.cell] += 1

	# Narrow-phase + tepki: kendi hucresi + 8 komsu, j > i, Gauss-Seidel
	var pairs := 0
	var responses := 0
	for i in range(n):
		var a: EntityS2 = _entities[i]
		var ci := a.cell
		var cx := ci % _cols
		@warning_ignore("integer_division")
		var cy := ci / _cols
		for ny in range(cy - 1, cy + 2):
			if ny < 0 or ny >= _rows:
				continue
			for nx in range(cx - 1, cx + 2):
				if nx < 0 or nx >= _cols:
					continue
				var c := ny * _cols + nx
				for k in range(_cell_start[c], _cell_start[c + 1]):
					var b: EntityS2 = _sorted[k]
					if b.index <= i:
						continue
					var d := b.pos - a.pos
					var d2 := d.length_squared()
					if d2 >= S2Params.D2_MAX:
						continue
					pairs += 1
					var dot := (b.vel - a.vel).dot(d)
					if dot < 0.0 and d2 > 0.0:
						# esit kutle, elastik: normal bilesenler degis tokus
						var fd := d * (dot / d2)
						a.vel += fd
						b.vel -= fd
						responses += 1
	_last_responses = responses
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

	var pairs := _step_if_moved()
	_test_timer += delta
	_session.after_timer_advance(_test_timer, delta, get_window())

	# Ilk 3 saniye warmup — veriye dahil etme
	if _test_timer > 3.0:
		_stats.add_sample(delta * 1000.0)
		if pairs >= 0:
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
	print("[S2 OOP] Test basladi: %d entity, seed=%d" % [_session.args.n, _session.args.run_seed()])


func _validate_frame() -> void:
	var pairs := _step_if_moved()
	if pairs < 0:
		return
	_frame += 1
	_lines.append("%d,%d,%d" % [_frame, pairs, _last_responses])
	if _frame >= _session.args.frames:
		_validating = false
		var ok := S2Validation.write(_session.args.out_path, _lines, false)
		print("[S2 OOP] Dogrulama bitti: %d kare -> %s" % [_frame, _session.args.out_path])
		get_tree().quit(0 if ok else S1RunSession.EXIT_WRITE_FAILED)

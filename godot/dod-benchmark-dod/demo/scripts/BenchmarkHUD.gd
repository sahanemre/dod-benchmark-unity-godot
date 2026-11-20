class_name BenchmarkHUD extends CanvasLayer

## Benchmark kontrol paneli.
## Tek sorumluluk: kullanici arayuzu.
## Mantik tasimaz; durum disaridan set edilir, aksiyonlar signal ile bildirilir.

signal select_pressed(index: int)
signal start_single_pressed
signal start_all_pressed

var _entity_buttons: Array[Button] = []
var _start_single_btn: Button
var _start_all_btn: Button
var _testing_label: Label
var _fps_label: Label
var _frame_time_label: Label
var _entity_count_label: Label
var _status_label: Label
var _selected_index: int = 0
var _interactive: bool = true


func setup(entity_counts: Array[int]) -> void:
	var panel := PanelContainer.new()
	panel.position = Vector2(10, 10)
	add_child(panel)

	var vbox := VBoxContainer.new()
	vbox.custom_minimum_size = Vector2(360, 0)
	panel.add_child(vbox)

	var title := Label.new()
	title.text = "DOD Benchmark — Godot DOD (native C++)"
	title.add_theme_color_override("font_color", Color.MEDIUM_PURPLE)
	vbox.add_child(title)

	vbox.add_child(HSeparator.new())

	var count_lbl := Label.new()
	count_lbl.text = "Entity Sayisi:"
	vbox.add_child(count_lbl)

	var btn_row := HBoxContainer.new()
	vbox.add_child(btn_row)
	for i in range(entity_counts.size()):
		var btn := Button.new()
		btn.text = str(entity_counts[i] / 1000) + "K"
		btn.custom_minimum_size = Vector2(58, 30)
		var idx := i
		btn.pressed.connect(func(): _on_select(idx))
		btn_row.add_child(btn)
		_entity_buttons.append(btn)

	vbox.add_child(HSeparator.new())

	var action_row := HBoxContainer.new()
	vbox.add_child(action_row)

	_start_single_btn = Button.new()
	_start_single_btn.text = "Tek Test"
	_start_single_btn.custom_minimum_size = Vector2(165, 32)
	_start_single_btn.pressed.connect(func(): start_single_pressed.emit())
	action_row.add_child(_start_single_btn)

	_start_all_btn = Button.new()
	_start_all_btn.text = "Tum Testler"
	_start_all_btn.custom_minimum_size = Vector2(165, 32)
	_start_all_btn.pressed.connect(func(): start_all_pressed.emit())
	action_row.add_child(_start_all_btn)

	_testing_label = Label.new()
	_testing_label.text = ">>> Test calisiyor..."
	_testing_label.add_theme_color_override("font_color", Color.ORANGE)
	_testing_label.visible = false
	vbox.add_child(_testing_label)

	vbox.add_child(HSeparator.new())

	_fps_label = Label.new()
	vbox.add_child(_fps_label)
	_frame_time_label = Label.new()
	vbox.add_child(_frame_time_label)
	_entity_count_label = Label.new()
	vbox.add_child(_entity_count_label)

	vbox.add_child(HSeparator.new())

	_status_label = Label.new()
	_status_label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	_status_label.custom_minimum_size = Vector2(350, 55)
	vbox.add_child(_status_label)

	_update_selection()


func _on_select(index: int) -> void:
	_selected_index = index
	_update_selection()
	select_pressed.emit(index)


func _update_selection() -> void:
	for i in range(_entity_buttons.size()):
		if i == _selected_index:
			_entity_buttons[i].add_theme_color_override("font_color", Color.MEDIUM_PURPLE)
		else:
			_entity_buttons[i].remove_theme_color_override("font_color")


## Native eklenti yuklenemediyse paneli kilitler ve sebebi gosterir.
func set_unavailable(reason: String) -> void:
	_interactive = false
	if _start_single_btn: _start_single_btn.disabled = true
	if _start_all_btn: _start_all_btn.disabled = true
	for b in _entity_buttons:
		b.disabled = true
	set_status(reason)


func set_testing(is_testing: bool) -> void:
	if not _interactive:
		return
	_start_single_btn.visible = not is_testing
	_start_all_btn.visible = not is_testing
	_testing_label.visible = is_testing


func update_metrics(fps: float, frame_time_ms: float, active_count: int) -> void:
	_fps_label.text = "FPS: %.0f" % fps
	_frame_time_label.text = "Frame Time: %.2f ms" % frame_time_ms
	_entity_count_label.text = "Aktif Entity: %d" % active_count


func set_status(text: String) -> void:
	_status_label.text = text


func get_selected_index() -> int:
	return _selected_index

class_name FrameStatistics extends RefCounted

## Frame time orneklerini toplar ve istatistik hesaplar.
## Tek sorumluluk: olcum verisi biriktirme ve ozetleme.

var _samples: Array[float] = []


func reset() -> void:
	_samples.clear()


func add_sample(frame_time_ms: float) -> void:
	_samples.append(frame_time_ms)


func get_sample_count() -> int:
	return _samples.size()


func compute(entity_count: int, memory_mb: float, duration: float) -> BenchmarkResult:
	var result := BenchmarkResult.new()
	if _samples.is_empty():
		return result

	var total := 0.0
	var min_val := _samples[0]
	var max_val := _samples[0]
	for s in _samples:
		total += s
		if s < min_val: min_val = s
		if s > max_val: max_val = s

	var avg := total / _samples.size()
	var variance := 0.0
	for s in _samples:
		var diff := s - avg
		variance += diff * diff

	result.entity_count = entity_count
	result.avg_frame_time = avg
	result.min_frame_time = min_val
	result.max_frame_time = max_val
	result.std_dev = sqrt(variance / _samples.size())
	result.avg_fps = 1000.0 / avg
	result.min_fps = 1000.0 / max_val
	result.memory_mb = memory_mb
	result.total_frames = _samples.size()
	result.duration = duration
	return result

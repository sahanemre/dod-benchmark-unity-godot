class_name BenchmarkResult extends RefCounted

## Tek bir test kosusunun sonucu. Sadece veri tasir, davranis yok.

var entity_count: int = 0
var avg_frame_time: float = 0.0
var min_frame_time: float = 0.0
var max_frame_time: float = 0.0
var std_dev: float = 0.0
var avg_fps: float = 0.0
var min_fps: float = 0.0
var memory_mb: float = 0.0
var total_frames: int = 0
var duration: float = 0.0

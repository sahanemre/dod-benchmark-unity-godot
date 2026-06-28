#include "movement_world.h"

#include <godot_cpp/core/class_db.hpp>

#include <cmath>

using namespace godot;

// --- hafif xorshift RNG -----------------------------------------------------
inline float MovementWorld::next_float() {
	// xorshift32 -> [0,1)
	rng_state ^= rng_state << 13;
	rng_state ^= rng_state >> 17;
	rng_state ^= rng_state << 5;
	return (rng_state & 0x00FFFFFFu) / static_cast<float>(0x01000000u);
}

inline float MovementWorld::range_float(float lo, float hi) {
	return lo + (hi - lo) * next_float();
}

// --- yasam dongusu ----------------------------------------------------------
MovementWorld::MovementWorld() {}
MovementWorld::~MovementWorld() {}

void MovementWorld::spawn(int p_count, float p_speed, Vector2 screen_min, Vector2 screen_max) {
	entity_count = p_count < 0 ? 0 : p_count;

	pos_x.resize(entity_count);
	pos_y.resize(entity_count);
	vel_x.resize(entity_count);
	vel_y.resize(entity_count);
	col_r.resize(entity_count);
	col_g.resize(entity_count);
	col_b.resize(entity_count);

	const float two_pi = 6.28318530718f;
	for (int i = 0; i < entity_count; ++i) {
		pos_x[i] = range_float(screen_min.x, screen_max.x);
		pos_y[i] = range_float(screen_min.y, screen_max.y);

		float angle = range_float(0.0f, two_pi);
		// cos/sin yerine ucuz yaklasim gerekmez; std cukur degil burada
		vel_x[i] = cosf(angle) * p_speed;
		vel_y[i] = sinf(angle) * p_speed;

		col_r[i] = range_float(0.3f, 1.0f);
		col_g[i] = range_float(0.3f, 1.0f);
		col_b[i] = range_float(0.3f, 1.0f);
	}
}

void MovementWorld::update(double delta, Vector2 screen_min, Vector2 screen_max) {
	const float dt = static_cast<float>(delta);
	const float min_x = screen_min.x;
	const float min_y = screen_min.y;
	const float max_x = screen_max.x;
	const float max_y = screen_max.y;

	// Sicak dongu: SoA sayesinde tamamen ardisik bellek erisimi.
	for (int i = 0; i < entity_count; ++i) {
		float px = pos_x[i] + vel_x[i] * dt;
		float py = pos_y[i] + vel_y[i] * dt;

		if (px < min_x) { px = min_x; vel_x[i] = -vel_x[i]; }
		else if (px > max_x) { px = max_x; vel_x[i] = -vel_x[i]; }

		if (py < min_y) { py = min_y; vel_y[i] = -vel_y[i]; }
		else if (py > max_y) { py = max_y; vel_y[i] = -vel_y[i]; }

		pos_x[i] = px;
		pos_y[i] = py;
	}
}

PackedFloat32Array MovementWorld::get_buffer() {
	// MultiMesh 2B (TRANSFORM_2D) + use_colors: instance basina 12 float.
	// Transform2D 8 float olarak saklanir (2x4 matris), ardindan RGBA 4 float.
	buffer.resize(entity_count * 12);
	float *w = buffer.ptrw();

	for (int i = 0; i < entity_count; ++i) {
		int o = i * 12;
		// basis = birim (donme/olcek yok), origin = (px, py)
		w[o + 0] = 1.0f; w[o + 1] = 0.0f; w[o + 2] = 0.0f; w[o + 3] = pos_x[i];
		w[o + 4] = 0.0f; w[o + 5] = 1.0f; w[o + 6] = 0.0f; w[o + 7] = pos_y[i];
		// renk
		w[o + 8] = col_r[i]; w[o + 9] = col_g[i]; w[o + 10] = col_b[i]; w[o + 11] = 1.0f;
	}
	return buffer;
}

int MovementWorld::get_count() const {
	return entity_count;
}

void MovementWorld::clear() {
	entity_count = 0;
	pos_x.clear(); pos_y.clear();
	vel_x.clear(); vel_y.clear();
	col_r.clear(); col_g.clear(); col_b.clear();
	buffer.resize(0);
}

void MovementWorld::_bind_methods() {
	ClassDB::bind_method(D_METHOD("spawn", "count", "speed", "screen_min", "screen_max"), &MovementWorld::spawn);
	ClassDB::bind_method(D_METHOD("update", "delta", "screen_min", "screen_max"), &MovementWorld::update);
	ClassDB::bind_method(D_METHOD("get_buffer"), &MovementWorld::get_buffer);
	ClassDB::bind_method(D_METHOD("get_count"), &MovementWorld::get_count);
	ClassDB::bind_method(D_METHOD("clear"), &MovementWorld::clear);
}

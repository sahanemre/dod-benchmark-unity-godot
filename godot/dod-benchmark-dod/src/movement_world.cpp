#include "movement_world.h"

#include <godot_cpp/core/class_db.hpp>

#include <algorithm>
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

void MovementWorld::set_seed(int64_t p_seed) {
	// S1 parity profili: seed = 1000 + run_index. Ardisik seed'ler xorshift'te
	// iliskili baslangiclar verir; murmur3 fmix32 ile karistirilir. Cagrilmazsa
	// (published) RNG orijinal sabit baslangic durumuyla calisir.
	uint32_t h = static_cast<uint32_t>(p_seed);
	h ^= h >> 16;
	h *= 0x85ebca6bu;
	h ^= h >> 13;
	h *= 0xc2b2ae35u;
	h ^= h >> 16;
	rng_state = (h != 0u) ? h : 0x9e3779b9u;
}

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

// --- S2: carpisma -------------------------------------------------------------
// Algoritma tools/s2_reference/s2_reference.c ile birebir ayni (islem sirasi dahil).

void MovementWorld::spawn_s2(int p_count, float p_speed, Vector2 p_min, Vector2 p_max, int64_t p_seed) {
	set_seed(p_seed);
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
		pos_x[i] = range_float(p_min.x, p_max.x);
		pos_y[i] = range_float(p_min.y, p_max.y);
		float angle = range_float(0.0f, two_pi);
		// Dort dilde ayni sonuc icin cos/sin double'da hesaplanip float'a cevrilir.
		vel_x[i] = static_cast<float>(std::cos(static_cast<double>(angle))) * p_speed;
		vel_y[i] = static_cast<float>(std::sin(static_cast<double>(angle))) * p_speed;
		col_r[i] = range_float(0.3f, 1.0f);
		col_g[i] = range_float(0.3f, 1.0f);
		col_b[i] = range_float(0.3f, 1.0f);
	}
}

int MovementWorld::collide(Vector2 p_min, Vector2 p_max, float p_cell) {
	const float min_x = p_min.x;
	const float min_y = p_min.y;
	const int cols = static_cast<int>(std::ceil((p_max.x - p_min.x) / p_cell));
	const int rows = static_cast<int>(std::ceil((p_max.y - p_min.y) / p_cell));
	const int ncells = cols * rows;
	const float d2_max = p_cell * p_cell; // (2r)^2, hucre = 2r
	const int n = entity_count;

	if (static_cast<int>(s2_cell_of.size()) < n) {
		s2_cell_of.resize(n);
		s2_sorted.resize(n);
	}
	if (static_cast<int>(s2_cell_start.size()) != ncells + 1) {
		s2_cell_start.resize(ncells + 1);
		s2_cell_fill.resize(ncells);
	}

	// Broad-phase: counting sort (sayim -> prefix sum -> indeks dizisi)
	std::fill(s2_cell_start.begin(), s2_cell_start.end(), 0);
	for (int i = 0; i < n; ++i) {
		int cx = static_cast<int>((pos_x[i] - min_x) / p_cell);
		int cy = static_cast<int>((pos_y[i] - min_y) / p_cell);
		cx = cx < 0 ? 0 : (cx >= cols ? cols - 1 : cx);
		cy = cy < 0 ? 0 : (cy >= rows ? rows - 1 : cy);
		const int c = cy * cols + cx;
		s2_cell_of[i] = c;
		s2_cell_start[c + 1]++;
	}
	for (int c = 0; c < ncells; ++c) {
		s2_cell_start[c + 1] += s2_cell_start[c];
	}
	std::copy(s2_cell_start.begin(), s2_cell_start.begin() + ncells, s2_cell_fill.begin());
	for (int i = 0; i < n; ++i) {
		s2_sorted[s2_cell_fill[s2_cell_of[i]]++] = i;
	}

	// Narrow-phase + tepki: kendi hucresi + 8 komsu, j > i, Gauss-Seidel
	int pairs = 0;
	int responses = 0;
	for (int i = 0; i < n; ++i) {
		const int ci = s2_cell_of[i];
		const int cx = ci % cols;
		const int cy = ci / cols;
		for (int ny = cy - 1; ny <= cy + 1; ++ny) {
			if (ny < 0 || ny >= rows) continue;
			for (int nx = cx - 1; nx <= cx + 1; ++nx) {
				if (nx < 0 || nx >= cols) continue;
				const int c = ny * cols + nx;
				const int end = s2_cell_start[c + 1];
				for (int k = s2_cell_start[c]; k < end; ++k) {
					const int j = s2_sorted[k];
					if (j <= i) continue;
					const float dx = pos_x[j] - pos_x[i];
					const float dy = pos_y[j] - pos_y[i];
					const float d2 = dx * dx + dy * dy;
					if (d2 >= d2_max) continue;
					pairs++;
					const float rvx = vel_x[j] - vel_x[i];
					const float rvy = vel_y[j] - vel_y[i];
					const float dot = rvx * dx + rvy * dy;
					if (dot < 0.0f && d2 > 0.0f) {
						// esit kutle, elastik: normal bilesenler degis tokus
						const float f = dot / d2;
						vel_x[i] += f * dx;
						vel_y[i] += f * dy;
						vel_x[j] -= f * dx;
						vel_y[j] -= f * dy;
						responses++;
					}
				}
			}
		}
	}
	s2_last_responses = responses;
	return pairs;
}

int MovementWorld::get_last_responses() const {
	return s2_last_responses;
}

int MovementWorld::count_pairs_bruteforce(float p_cell) const {
	const float d2_max = p_cell * p_cell;
	int pairs = 0;
	for (int i = 0; i < entity_count; ++i) {
		for (int j = i + 1; j < entity_count; ++j) {
			const float dx = pos_x[j] - pos_x[i];
			const float dy = pos_y[j] - pos_y[i];
			if (dx * dx + dy * dy < d2_max) pairs++;
		}
	}
	return pairs;
}

void MovementWorld::_bind_methods() {
	ClassDB::bind_method(D_METHOD("spawn", "count", "speed", "screen_min", "screen_max"), &MovementWorld::spawn);
	ClassDB::bind_method(D_METHOD("update", "delta", "screen_min", "screen_max"), &MovementWorld::update);
	ClassDB::bind_method(D_METHOD("get_buffer"), &MovementWorld::get_buffer);
	ClassDB::bind_method(D_METHOD("get_count"), &MovementWorld::get_count);
	ClassDB::bind_method(D_METHOD("clear"), &MovementWorld::clear);
	ClassDB::bind_method(D_METHOD("set_seed", "seed"), &MovementWorld::set_seed);
	ClassDB::bind_method(D_METHOD("spawn_s2", "count", "speed", "world_min", "world_max", "seed"), &MovementWorld::spawn_s2);
	ClassDB::bind_method(D_METHOD("collide", "world_min", "world_max", "cell"), &MovementWorld::collide);
	ClassDB::bind_method(D_METHOD("get_last_responses"), &MovementWorld::get_last_responses);
	ClassDB::bind_method(D_METHOD("count_pairs_bruteforce", "cell"), &MovementWorld::count_pairs_bruteforce);
}

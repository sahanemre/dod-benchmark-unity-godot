#ifndef MOVEMENT_WORLD_H
#define MOVEMENT_WORLD_H

#include <godot_cpp/classes/ref_counted.hpp>
#include <godot_cpp/variant/packed_float32_array.hpp>
#include <godot_cpp/variant/vector2.hpp>

#include <cstdint>
#include <vector>

namespace godot {

// MovementWorld — DOD veri dunyasi (native C++ GDExtension).
//
// Tezin kalbi: Struct of Arrays (SoA). Her alan kendi bitisik dizisinde
// tutulur; bir entity "nesnesi" yoktur. Pozisyon guncelleme dongusu
// pos_x / pos_y / vel_x / vel_y dizilerini ardisik tarar, boylece
// islemci onbellegi (cache) en verimli sekilde kullanilir.
//
// Tek sorumluluk: SoA veri + toplu hareket hesabi. Render Godot tarafinda
// (GDScript + MultiMesh) yapilir; bu sinif yalnizca MultiMesh'in
// bekledigi 2B transform+renk buffer'ini uretir.
class MovementWorld : public RefCounted {
	GDCLASS(MovementWorld, RefCounted)

private:
	// SoA — AoS (Array of Structs / OOP nesneleri) DEGIL
	std::vector<float> pos_x;
	std::vector<float> pos_y;
	std::vector<float> vel_x;
	std::vector<float> vel_y;
	std::vector<float> col_r;
	std::vector<float> col_g;
	std::vector<float> col_b;
	int entity_count = 0;

	// Hafif, hizli RNG (xorshift) — std::mt19937'den daha ucuz
	uint32_t rng_state = 0x9e3779b9u;
	inline float next_float();
	inline float range_float(float lo, float hi);

	// MultiMesh 2B buffer'i (instance basina 12 float: 8 transform + 4 renk),
	// her frame yeniden kullanilir; surekli yeniden tahsis edilmez.
	PackedFloat32Array buffer;

	// S2 grid tamponlari: kare basina yeniden tahsis edilmez (counting sort).
	std::vector<int> s2_cell_of;
	std::vector<int> s2_cell_start;   // hucre sayisi + 1 (prefix sum)
	std::vector<int> s2_cell_fill;
	std::vector<int> s2_sorted;       // hucreye gore sirali entity indeksleri
	int s2_last_responses = 0;

protected:
	static void _bind_methods();

public:
	MovementWorld();
	~MovementWorld();

	// Entity dizilerini olusturur ve rastgele baslangic degerleriyle doldurur.
	void spawn(int p_count, float p_speed, Vector2 screen_min, Vector2 screen_max);

	// Hareket sistemi: tum entity'leri tek SoA dongusunde gunceller (bounce).
	void update(double delta, Vector2 screen_min, Vector2 screen_max);

	// MultiMesh.buffer icin 2B transform+renk dizisi uretir (12 float/instance).
	PackedFloat32Array get_buffer();

	int get_count() const;
	void clear();

	// RNG baslangic durumu (S1 parity: seed = 1000 + run_index). Hareket
	// dongusunu etkilemez; yalnizca spawn'daki baslangic degerlerini belirler.
	void set_seed(int64_t p_seed);

	// ---- S2 (carpisma) -------------------------------------------------
	// S1 metotlarina dokunulmaz; S2 hareket icin yine update() kullanir.

	// S2 spawn: dort konfigurasyonda birebir ayni baslangic (ortak RNG, hiz
	// (float)cos((double)aci) * hiz). Entity basina 6 RNG cekilisi: x, y, aci, r, g, b.
	void spawn_s2(int p_count, float p_speed, Vector2 p_min, Vector2 p_max, int64_t p_seed);

	// Broad-phase (uniform grid, counting sort) + narrow-phase + elastik tepki.
	// Kesisen cift sayisini dondurur; tepki verilen cift sayisi get_last_responses().
	int collide(Vector2 p_min, Vector2 p_max, float p_cell);
	int get_last_responses() const;

	// Dogrulama: O(N^2) kesisen cift sayisi (yalnizca sayar, hizlara dokunmaz).
	int count_pairs_bruteforce(float p_cell) const;
};

} // namespace godot

#endif // MOVEMENT_WORLD_H

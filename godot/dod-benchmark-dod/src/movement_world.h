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
};

} // namespace godot

#endif // MOVEMENT_WORLD_H

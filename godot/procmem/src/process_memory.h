#ifndef PROCESS_MEMORY_H
#define PROCESS_MEMORY_H

#include <godot_cpp/classes/ref_counted.hpp>

#include <cstdint>

namespace godot {

// ProcessMemory — isletim sistemi duzeyinde bellek olcumu (GDExtension).
//
// GDScript isletim sistemi API'lerine erisemedigi icin bu kucuk eklenti
// gerekir. Hem Godot OOP hem Godot DOD projesi AYNI eklentiyi kullanir;
// Unity projeleri ayni Windows cagrisini P/Invoke ile yapar. Boylece dort
// konfigurasyonda da ayni OS yontemi kullanilir.
//
// Windows: GetProcessMemoryInfo -> PROCESS_MEMORY_COUNTERS_EX.PrivateUsage
// (perfmon "Private Bytes"). Diger platformlarda -1 doner.
//
// Tek sorumluluk: olcum. Benchmark'in sicak dongusunde CAGRILMAZ; kosu
// basina yalnizca bir kez (warmup bitiminde) okunur.
class ProcessMemory : public RefCounted {
	GDCLASS(ProcessMemory, RefCounted)

protected:
	static void _bind_methods();

public:
	// Surecin private bytes degeri; okunamazsa -1.
	int64_t get_private_bytes() const;
};

} // namespace godot

#endif // PROCESS_MEMORY_H

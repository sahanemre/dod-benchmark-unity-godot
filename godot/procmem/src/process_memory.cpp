#include "process_memory.h"

#include <godot_cpp/core/class_db.hpp>

// windows.h godot-cpp basliklarindan SONRA ve NOMINMAX ile eklenir;
// aksi halde min/max makrolari sablon kodlariyla cakisir.
#ifdef _WIN32
#ifndef NOMINMAX
#define NOMINMAX
#endif
#ifndef WIN32_LEAN_AND_MEAN
#define WIN32_LEAN_AND_MEAN
#endif
#include <windows.h>
#include <psapi.h>
#endif

using namespace godot;

void ProcessMemory::_bind_methods() {
	ClassDB::bind_method(D_METHOD("get_private_bytes"), &ProcessMemory::get_private_bytes);
}

int64_t ProcessMemory::get_private_bytes() const {
#ifdef _WIN32
	PROCESS_MEMORY_COUNTERS_EX pmc;
	ZeroMemory(&pmc, sizeof(pmc));
	pmc.cb = sizeof(pmc);
	if (GetProcessMemoryInfo(GetCurrentProcess(),
				reinterpret_cast<PROCESS_MEMORY_COUNTERS *>(&pmc), sizeof(pmc))) {
		return static_cast<int64_t>(pmc.PrivateUsage);
	}
	return -1;
#else
	return -1;
#endif
}
